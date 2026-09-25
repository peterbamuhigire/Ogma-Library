namespace OgmaLibrary.Application.Ocr;

/// <summary>
/// Stable OCR failure codes recorded on the job (<c>Jobs.FailureCode</c>) and mapped to
/// localised messages (Sept-23 Phase 17, task 4). Codes never carry paths or page text.
/// </summary>
public static class OcrFailureCodes
{
    /// <summary>The OCR engine or PDF worker exceeded its time limit.</summary>
    public const string Timeout = "ocr_timeout";

    /// <summary>A page or document exceeded the OCR resource limits.</summary>
    public const string ResourceLimit = "ocr_resource_limit";

    /// <summary>The trained data for the requested language is missing or failed its checksum.</summary>
    public const string MissingLanguageData = "ocr_language_data_missing";

    /// <summary>A page could not be rendered or recognised.</summary>
    public const string UnreadablePage = "ocr_unreadable_page";

    /// <summary>The source PDF is no longer available.</summary>
    public const string FileUnavailable = "ocr_file_unavailable";

    /// <summary>The job payload is invalid.</summary>
    public const string InvalidJob = "ocr_invalid_payload";

    /// <summary>An unexpected failure; the log has the details.</summary>
    public const string Unexpected = "ocr_processing_failed";

    /// <summary>All codes that have a localised message.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Timeout,
        ResourceLimit,
        MissingLanguageData,
        UnreadablePage,
        FileUnavailable,
        InvalidJob,
        Unexpected,
    ];

    /// <summary>Whether a retry can help (a missing language pack or a bad payload will not change).</summary>
    public static bool IsRetryable(string code) =>
        code is not (MissingLanguageData or InvalidJob or ResourceLimit);

    /// <summary>
    /// Localisation key for a failure code (<c>Ocr.Failure.&lt;code&gt;</c>), or the
    /// unexpected-failure key for codes this build does not know.
    /// </summary>
    public static string LocalizationKey(string? code) =>
        $"Ocr.Failure.{(code is not null && All.Contains(code, StringComparer.Ordinal) ? code : Unexpected)}";

    /// <summary>Whether a job failure code belongs to OCR.</summary>
    public static bool IsOcrCode(string? code) =>
        code is not null && code.StartsWith("ocr_", StringComparison.Ordinal);
}

/// <summary>
/// User OCR policy (Sept-23 Phase 17, task 3). Phase 08 exposes it under
/// Settings → Reading. Auto OCR is on by default on this local-only engine; it is bounded by the
/// job runtime's one-at-a-time document-render group and pauses on battery power.
/// </summary>
/// <param name="AutoOcrScannedBooks">Queue OCR automatically for image-only books.</param>
/// <param name="Language">Tesseract language selector, for example <c>eng</c>.</param>
/// <param name="PauseOnBattery">Do not start automatic OCR while the device runs on battery.</param>
public sealed record OcrPolicySettings(
    bool AutoOcrScannedBooks = true,
    string Language = "eng",
    bool PauseOnBattery = true)
{
    /// <summary>The default policy.</summary>
    public static OcrPolicySettings Default { get; } = new();
}

/// <summary>Loads and saves the OCR policy.</summary>
public interface IOcrPolicySettingsStore
{
    /// <summary>Returns the validated policy, falling back to <see cref="OcrPolicySettings.Default"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The policy.</returns>
    Task<OcrPolicySettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically persists the policy.</summary>
    /// <param name="settings">The policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveAsync(OcrPolicySettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Reports whether the device currently runs on battery power.</summary>
public interface IPowerSource
{
    /// <summary>True when the OS reports battery power; false when on AC or unknown.</summary>
    bool IsOnBattery { get; }
}

/// <summary>
/// OCR languages that can actually run on this installation: the trained data is present
/// and matches its pinned checksum (Sept-23 Phase 17, task 5).
/// </summary>
public interface IOcrLanguageCatalog
{
    /// <summary>Returns the installed, verified language keys, for example <c>eng</c>.</summary>
    /// <returns>The installed languages.</returns>
    IReadOnlyList<string> GetInstalledLanguages();
}

/// <summary>Queues OCR automatically for scanned books according to the user policy.</summary>
public interface IOcrAutoPolicy
{
    /// <summary>
    /// Refreshes pending text statuses and, when the policy allows it, queues OCR for a bounded
    /// number of image-only books that have never been through OCR.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of books queued.</returns>
    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}

/// <summary>An OCR failure with a stable, localisable code (see <see cref="OcrFailureCodes"/>).</summary>
public sealed class OcrFailureException : Exception
{
    /// <summary>Initializes a new instance with the unexpected-failure code.</summary>
    public OcrFailureException()
        : this(OcrFailureCodes.Unexpected)
    {
    }

    /// <summary>Initializes a new instance with a failure code.</summary>
    /// <param name="code">The stable failure code.</param>
    public OcrFailureException(string code)
        : this(code, null)
    {
    }

    /// <summary>Initializes a new instance with a failure code and cause.</summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="innerException">The underlying error.</param>
    public OcrFailureException(string code, Exception? innerException)
        : base($"OCR failed: {code}.", innerException)
    {
        Code = string.IsNullOrWhiteSpace(code) ? OcrFailureCodes.Unexpected : code;
    }

    /// <summary>The stable failure code.</summary>
    public string Code { get; }
}
