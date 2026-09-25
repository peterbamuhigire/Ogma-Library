using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Persists library root and excluded-folder settings to a JSON file in the
/// application data directory (FR-LIB-001). Thread-safe via a
/// <see cref="SemaphoreSlim"/> so concurrent read/write from background tasks is safe.
/// Sept-23 Phase 05 (K28): writes go to a temporary file that is flushed to disk and
/// atomically swapped in with a <c>.bak</c> copy of the previous version; a torn or
/// corrupt file is recovered from that copy instead of failing the caller.
/// </summary>
public sealed class LibrarySettingsService : ILibrarySettingsService, IDisposable
{
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly string _temporaryPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;

    // Serialization DTO — internal only, not part of the public contract.
    private sealed class SettingsDto
    {
        public string? LibraryRoot { get; set; }
        public List<string> ExcludedFolders { get; set; } = [];
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LibrarySettingsService"/>.
    /// </summary>
    /// <param name="dataDirectory">
    /// The directory under which <c>library-settings.json</c> is stored.
    /// The directory is created if it does not exist.
    /// </param>
    /// <param name="logger">Optional logger for recovery events.</param>
    public LibrarySettingsService(string dataDirectory, ILogger<LibrarySettingsService>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "library-settings.json");
        _backupPath = _settingsPath + ".bak";
        _temporaryPath = _settingsPath + ".tmp";
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default)
    {
        var dto = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return dto.LibraryRoot;
    }

    /// <inheritdoc />
    public async Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            dto.LibraryRoot = rootPath;
            await SaveLockedAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default)
    {
        var dto = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return dto.ExcludedFolders.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task SetExcludedFoldersAsync(
        IReadOnlyList<string> excludedFolders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excludedFolders);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            dto.ExcludedFolders = [.. excludedFolders];
            await SaveLockedAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public LibrarySettingsRecovery? LastRecovery { get; private set; }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();

    private async Task<SettingsDto> LoadAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<SettingsDto> LoadLockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            // A crash between the swap steps can leave only the backup behind.
            return File.Exists(_backupPath)
                ? await TryReadAsync(_backupPath, cancellationToken).ConfigureAwait(false) ?? new SettingsDto()
                : new SettingsDto();
        }

        try
        {
            return await ReadAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            SettingsDto? recovered = File.Exists(_backupPath)
                ? await TryReadAsync(_backupPath, cancellationToken).ConfigureAwait(false)
                : null;
            InfrastructureLog.SettingsRecovered(_logger, ex, recovered is not null);
            if (ex is not JsonException)
            {
                // A transient I/O failure (for example a file briefly locked by antivirus) must
                // never overwrite a valid settings file; use the backup for this read only.
                return recovered ?? new SettingsDto();
            }

            string? quarantined = QuarantineDamagedFile();
            LastRecovery = new LibrarySettingsRecovery(recovered is not null, quarantined);
            SettingsDto replacement = recovered ?? new SettingsDto();

            // Put the last good copy (or clean defaults) back so the next read is clean and
            // the recovery is reported once, not on every read.
            await SaveLockedAsync(replacement, cancellationToken, keepBackup: false).ConfigureAwait(false);
            return replacement;
        }
    }

    /// <summary>Keeps the damaged file for support under a timestamped name; returns its file name.</summary>
    private string? QuarantineDamagedFile()
    {
        string target = _settingsPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            File.Move(_settingsPath, target, overwrite: true);
            return Path.GetFileName(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InfrastructureLog.BestEffortStepFailed(_logger, ex, nameof(LibrarySettingsService), "settings.quarantine");
            return null;
        }
    }

    private static async Task<SettingsDto> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer
                .DeserializeAsync<SettingsDto>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? new SettingsDto();
        }
    }

    private async Task<SettingsDto?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            InfrastructureLog.BestEffortStepFailed(_logger, ex, nameof(LibrarySettingsService), "settings.read_backup");
            return null;
        }
    }

    private async Task SaveLockedAsync(
        SettingsDto dto,
        CancellationToken cancellationToken,
        bool keepBackup = true)
    {
        // 1. Write the complete document to a temporary file and flush it to disk.
        var stream = new FileStream(
            _temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(stream, dto, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        // 2. Swap it in atomically, keeping the previous version as the backup.
        if (File.Exists(_settingsPath) && keepBackup)
        {
            File.Replace(_temporaryPath, _settingsPath, _backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(_temporaryPath, _settingsPath, overwrite: true);
        }
    }
}
