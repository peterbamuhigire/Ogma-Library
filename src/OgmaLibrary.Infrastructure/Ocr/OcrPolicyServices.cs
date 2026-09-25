using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.Ocr;

/// <summary>
/// Lists the OCR languages whose trained data is present and matches its pinned SHA-256
/// (Sept-23 Phase 17, task 5). The check hashes each file once per process.
/// </summary>
public sealed class TesseractOcrLanguageCatalog : IOcrLanguageCatalog
{
    private readonly string _tessdataPath;
    private readonly Lazy<IReadOnlyList<string>> _installed;

    /// <summary>Initializes the catalogue over the app-local <c>tessdata</c> folder.</summary>
    public TesseractOcrLanguageCatalog()
        : this(Path.Combine(AppContext.BaseDirectory, "tessdata"))
    {
    }

    /// <summary>Initializes the catalogue over an explicit <c>tessdata</c> folder.</summary>
    /// <param name="tessdataPath">Directory containing <c>*.traineddata</c> files.</param>
    public TesseractOcrLanguageCatalog(string tessdataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tessdataPath);
        _tessdataPath = tessdataPath;
        _installed = new Lazy<IReadOnlyList<string>>(Discover, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetInstalledLanguages() => _installed.Value;

    private IReadOnlyList<string> Discover() =>
        [.. OcrLanguagePolicy.AllowedLanguages
            .Where(language => TesseractTrainingDataVerifier.Verify(_tessdataPath, language).IsValid)];
}

/// <summary>
/// JSON store for <see cref="OcrPolicySettings"/> in the app-data folder. Writes are atomic
/// (temp file then replace); an unreadable file falls back to the defaults.
/// </summary>
public sealed class JsonOcrPolicySettingsStore : IOcrPolicySettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly ILogger _logger;

    /// <summary>Initializes the store.</summary>
    /// <param name="path">Absolute path of <c>ocr-settings.json</c>.</param>
    /// <param name="logger">Optional logger.</param>
    public JsonOcrPolicySettingsStore(string path, ILogger<JsonOcrPolicySettingsStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<OcrPolicySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return OcrPolicySettings.Default;
        }

        try
        {
            FileStream stream = File.OpenRead(_path);
            await using (stream.ConfigureAwait(false))
            {
                OcrPolicySettings? settings = await JsonSerializer
                    .DeserializeAsync<OcrPolicySettings>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                return Validate(settings);
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            InfrastructureLog.SettingsRecovered(_logger, exception, recoveredFromBackup: false);
            return OcrPolicySettings.Default;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(OcrPolicySettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        OcrPolicySettings valid = Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temp = $"{_path}.{Guid.NewGuid():N}.tmp";
        FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, valid, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, _path, overwrite: true);
    }

    private static OcrPolicySettings Validate(OcrPolicySettings? settings)
    {
        if (settings is null)
        {
            return OcrPolicySettings.Default;
        }

        string language = OcrLanguagePolicy.Normalize(settings.Language) ?? OcrPolicySettings.Default.Language;
        return settings with { Language = language };
    }
}

/// <summary>Reads the Windows power state (<c>GetSystemPowerStatus</c>); other platforms report AC.</summary>
public sealed class SystemPowerSource : IPowerSource
{
    /// <inheritdoc />
    public bool IsOnBattery
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            // ACLineStatus: 0 = offline (battery), 1 = online, 255 = unknown.
            return GetSystemPowerStatus(out SystemPowerStatus status) && status.AcLineStatus == 0;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}

/// <summary>
/// Automatic OCR policy (Sept-23 Phase 17, task 3). Each sweep first assesses books whose text
/// status is still unknown, then, when the user policy allows it and the device is not on
/// battery, queues a bounded number of image-only books that have never been through OCR.
/// The OCR job type runs one at a time in the job runtime's <c>document-render</c> group, so
/// auto mode never runs more than one OCR worker process.
/// </summary>
public sealed class OcrAutoPolicyService : IOcrAutoPolicy
{
    /// <summary>Books queued at most per sweep.</summary>
    public const int MaximumBooksPerSweep = 10;

    /// <summary>Unknown statuses assessed at most per sweep.</summary>
    public const int MaximumStatusRefreshPerSweep = 50;

    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IOcrJobQueueService _queue;
    private readonly IOcrPolicySettingsStore _settings;
    private readonly IPowerSource _power;
    private readonly BookTextStatusService _textStatus;
    private readonly ILogger _logger;
    private bool _pausedLogged;

    /// <summary>Initializes the policy service.</summary>
    /// <param name="contextFactory">The catalogue context factory.</param>
    /// <param name="queue">The OCR queue.</param>
    /// <param name="settings">The OCR policy store.</param>
    /// <param name="power">The power source.</param>
    /// <param name="logger">Optional logger.</param>
    public OcrAutoPolicyService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IOcrJobQueueService queue,
        IOcrPolicySettingsStore settings,
        IPowerSource power,
        ILogger<OcrAutoPolicyService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(power);
        _contextFactory = contextFactory;
        _queue = queue;
        _settings = settings;
        _power = power;
        _textStatus = new BookTextStatusService(contextFactory);
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        await _textStatus.RefreshPendingAsync(MaximumStatusRefreshPerSweep, cancellationToken).ConfigureAwait(false);

        OcrPolicySettings policy = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.AutoOcrScannedBooks)
        {
            return 0;
        }

        if (policy.PauseOnBattery && _power.IsOnBattery)
        {
            if (!_pausedLogged)
            {
                InfrastructureLog.OcrAutoPausedOnBattery(_logger);
                _pausedLogged = true;
            }

            return 0;
        }

        _pausedLogged = false;
        List<string> candidates;
        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            // Only image-only books that were never through OCR: a failed or cancelled attempt
            // waits for the user ("Make searchable" or Retry), so auto mode cannot loop.
            candidates = await context.Books
                .AsNoTracking()
                .Where(book => book.Status == 0 &&
                    !book.IsPasswordProtected &&
                    book.TextStatus == (int)BookTextStatus.ImageOnly &&
                    !context.Jobs.Any(job => job.JobType == BookTextStatusService.OcrJobType && job.BookId == book.BookId))
                .OrderBy(book => book.BookId)
                .Select(book => book.BookId)
                .Take(MaximumBooksPerSweep)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int queued = 0;
        foreach (string bookId in candidates)
        {
            OcrQueueResult result = await _queue.QueueBookAsync(bookId, policy.Language, cancellationToken).ConfigureAwait(false);
            if (result.Queued)
            {
                queued++;
            }
        }

        if (queued > 0)
        {
            InfrastructureLog.OcrAutoQueued(_logger, queued);
        }

        return queued;
    }
}
