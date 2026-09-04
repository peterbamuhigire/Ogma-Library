using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>
/// Atomic, bounded local feedback store. It persists only a request hash and
/// constrained rating metadata, never the advisor prompt or generated answer.
/// </summary>
public sealed class AdvisorFeedbackService : IAdvisorFeedbackService, IDisposable
{
    private const int CurrentVersion = 1;
    private const int MaximumEntries = 10_000;
    private const int MaximumReasonLength = 64;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the feedback store at an application-data path.</summary>
    public AdvisorFeedbackService(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <inheritdoc />
    public async Task<AdvisorFeedbackEntry> SubmitAsync(
        AdvisorFeedbackEntry entry,
        bool consentGranted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!consentGranted)
        {
            throw new AdvisorFeedbackConsentRequiredException();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        AdvisorFeedbackEntry normalized = Normalize(entry, now);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AdvisorFeedbackEntry> entries = await ReadAsync(cancellationToken).ConfigureAwait(false);
            entries.RemoveAll(item => item.SubmittedUtc < now - Retention);
            if (entries.Count >= MaximumEntries)
            {
                throw new InvalidOperationException(
                    $"At most {MaximumEntries} advisor feedback entries may be retained.");
            }

            entries.Add(normalized);
            await WriteAsync(entries, cancellationToken).ConfigureAwait(false);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvisorFeedbackEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - Retention;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await ReadAsync(cancellationToken).ConfigureAwait(false))
                .Where(item => item.SubmittedUtc >= cutoff)
                .OrderByDescending(item => item.SubmittedUtc)
                .ThenBy(item => item.FeedbackId, StringComparer.Ordinal)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - Retention;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AdvisorFeedbackEntry> entries = await ReadAsync(cancellationToken).ConfigureAwait(false);
            int removed = entries.RemoveAll(item => item.SubmittedUtc < cutoff);
            if (removed > 0)
            {
                await WriteAsync(entries, cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<List<AdvisorFeedbackEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        using FileStream stream = File.OpenRead(_path);
        FeedbackDocument? document = await JsonSerializer.DeserializeAsync<FeedbackDocument>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return document?.Version == CurrentVersion
            ? document.Entries.ToList()
            : [];
    }

    private async Task WriteAsync(
        IReadOnlyList<AdvisorFeedbackEntry> entries,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        new FeedbackDocument(CurrentVersion, entries),
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static AdvisorFeedbackEntry Normalize(
        AdvisorFeedbackEntry entry,
        DateTimeOffset submittedUtc)
    {
        string feedbackId = NormalizeToken(entry.FeedbackId, nameof(entry.FeedbackId), 128);
        string requestHash = NormalizeToken(entry.RequestHash, nameof(entry.RequestHash), 64);
        if (requestHash.Length != 64 || !requestHash.All(IsHex))
        {
            throw new ArgumentException("RequestHash must be a SHA-256 hex digest.", nameof(entry));
        }

        if (entry.Rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), "Advisor feedback rating must be between 1 and 5.");
        }

        string? reason = string.IsNullOrWhiteSpace(entry.ReasonCode)
            ? null
            : NormalizeToken(entry.ReasonCode, nameof(entry.ReasonCode), MaximumReasonLength);
        return new AdvisorFeedbackEntry(feedbackId, requestHash.ToLowerInvariant(), entry.Rating, reason, submittedUtc);
    }

    private static string NormalizeToken(string? value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Feedback value is outside the bounded contract.", parameterName);
        }

        return normalized;
    }

    private static bool IsHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private sealed record FeedbackDocument(
        int Version,
        IReadOnlyList<AdvisorFeedbackEntry> Entries);
}
