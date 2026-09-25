using Microsoft.Extensions.Logging;

namespace OgmaLibrary.Infrastructure.Diagnostics;

/// <summary>
/// Source-generated log events shared by Infrastructure and Workers services (Sept-23 Phase 02,
/// T02.4). Messages name the component and step only; file names, titles and payloads are never
/// passed, and the file sink redacts anything that slips through.
/// </summary>
public static partial class InfrastructureLog
{
    /// <summary>A best-effort step failed and was intentionally ignored.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The ignored failure.</param>
    /// <param name="component">The component name.</param>
    /// <param name="step">A stable <c>area.step</c> name.</param>
    [LoggerMessage(EventId = 5001, EventName = "infrastructure.step.ignored", Level = LogLevel.Debug,
        Message = "{Component} best-effort step {Step} failed and was ignored")]
    public static partial void BestEffortStepFailed(ILogger logger, Exception exception, string component, string step);

    /// <summary>An operation failed and the service degraded to a safe fallback.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="component">The component name.</param>
    /// <param name="step">A stable <c>area.step</c> name.</param>
    [LoggerMessage(EventId = 5002, EventName = "infrastructure.step.degraded", Level = LogLevel.Warning,
        Message = "{Component} step {Step} failed; continuing with a safe fallback")]
    public static partial void DegradedStep(ILogger logger, Exception exception, string component, string step);

    /// <summary>A recovery or audit step failed after another failure.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="component">The component name.</param>
    /// <param name="step">A stable <c>area.step</c> name.</param>
    [LoggerMessage(EventId = 5003, EventName = "infrastructure.recovery.failed", Level = LogLevel.Error,
        Message = "{Component} recovery step {Step} failed")]
    public static partial void RecoveryStepFailed(ILogger logger, Exception exception, string component, string step);

    /// <summary>A background job loop iteration failed and will retry.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="worker">The worker name.</param>
    [LoggerMessage(EventId = 5004, EventName = "job.retry.scheduled", Level = LogLevel.Warning,
        Message = "{Worker} iteration failed; retrying after a delay")]
    public static partial void WorkerRetryScheduled(ILogger logger, Exception exception, string worker);

    /// <summary>A library scan reached a terminal state (Sept-23 Phase 05).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="outcome">The terminal outcome.</param>
    /// <param name="added">Books added.</param>
    /// <param name="updated">Books updated.</param>
    /// <param name="needsAttention">Files that need attention.</param>
    /// <param name="missing">Files flagged missing.</param>
    /// <param name="restored">Files restored.</param>
    /// <param name="roots">Roots scanned.</param>
    /// <param name="elapsedMs">Elapsed milliseconds.</param>
    [LoggerMessage(EventId = 5010, EventName = "library.scan.finished", Level = LogLevel.Information,
        Message = "Library scan finished {Outcome}: added={Added} updated={Updated} needsAttention={NeedsAttention} missing={Missing} restored={Restored} roots={Roots} elapsedMs={ElapsedMs}")]
    public static partial void LibraryScanFinished(
        ILogger logger,
        OgmaLibrary.Application.Ingestion.ScanOutcome outcome,
        int added,
        int updated,
        int needsAttention,
        int missing,
        int restored,
        int roots,
        long elapsedMs);

    /// <summary>A library scan failed with an unexpected error (Sept-23 Phase 05).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    [LoggerMessage(EventId = 5011, EventName = "library.scan.failed", Level = LogLevel.Error,
        Message = "Library scan failed")]
    public static partial void LibraryScanFailed(ILogger logger, Exception exception);

    /// <summary>A file watcher overflowed or failed and fell back to a scan (Sept-23 Phase 05).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The watcher error.</param>
    [LoggerMessage(EventId = 5012, EventName = "library.watcher.fallback", Level = LogLevel.Warning,
        Message = "Library folder watcher failed or overflowed; falling back to an incremental scan")]
    public static partial void LibraryWatcherFallback(ILogger logger, Exception exception);

    /// <summary>Legacy in-library asset folders were migrated to app data (Sept-23 Phase 05).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="copied">Files copied and verified.</param>
    /// <param name="removed">Source files removed after verification.</param>
    /// <param name="kept">Files left in place (unknown or unverifiable).</param>
    [LoggerMessage(EventId = 5013, EventName = "library.assets.migrated", Level = LogLevel.Information,
        Message = "Legacy library asset folders migrated: copied={Copied} removed={Removed} kept={Kept}")]
    public static partial void LegacyAssetsMigrated(ILogger logger, int copied, int removed, int kept);

    /// <summary>A settings file was corrupt and was recovered from its backup (Sept-23 Phase 05).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The parse failure.</param>
    /// <param name="recoveredFromBackup">Whether the backup copy was usable.</param>
    [LoggerMessage(EventId = 5014, EventName = "settings.recovered", Level = LogLevel.Warning,
        Message = "A settings file could not be read; recoveredFromBackup={RecoveredFromBackup}")]
    public static partial void SettingsRecovered(ILogger logger, Exception exception, bool recoveredFromBackup);

    /// <summary>An OCR job failed with a typed reason (Sept-23 Phase 17, task 4).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The underlying error, when one was thrown.</param>
    /// <param name="jobId">The OCR job id.</param>
    /// <param name="failureCode">The stable failure code.</param>
    /// <param name="retryable">Whether the retry policy may run it again.</param>
    [LoggerMessage(EventId = 5020, EventName = "ocr.job.failed", Level = LogLevel.Warning,
        Message = "OCR job {JobId} failed: {FailureCode} (retryable={Retryable})")]
    public static partial void OcrJobFailed(ILogger logger, Exception? exception, long jobId, string failureCode, bool retryable);

    /// <summary>An OCR job finished (Sept-23 Phase 17).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="jobId">The OCR job id.</param>
    /// <param name="pagesRecognised">Pages sent through OCR by this run.</param>
    /// <param name="chunks">Search chunks written for the book.</param>
    /// <param name="textStatus">The book's resulting text status.</param>
    [LoggerMessage(EventId = 5021, EventName = "ocr.job.completed", Level = LogLevel.Information,
        Message = "OCR job {JobId} completed: pages={PagesRecognised} chunks={Chunks} textStatus={TextStatus}")]
    public static partial void OcrJobCompleted(ILogger logger, long jobId, int pagesRecognised, int chunks, OgmaLibrary.Application.Ocr.BookTextStatus textStatus);

    /// <summary>The automatic OCR policy queued scanned books (Sept-23 Phase 17, task 3).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="queued">Books queued by this sweep.</param>
    [LoggerMessage(EventId = 5022, EventName = "ocr.auto.queued", Level = LogLevel.Information,
        Message = "Automatic OCR queued {Queued} scanned book(s)")]
    public static partial void OcrAutoQueued(ILogger logger, int queued);

    /// <summary>Automatic OCR is waiting because the device runs on battery (Sept-23 Phase 17).</summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(EventId = 5023, EventName = "ocr.auto.paused_on_battery", Level = LogLevel.Information,
        Message = "Automatic OCR is paused while the device runs on battery power")]
    public static partial void OcrAutoPausedOnBattery(ILogger logger);
}
