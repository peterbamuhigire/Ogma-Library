using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>What <see cref="UiThreadGuard"/> does when a view model is mutated off the UI thread.</summary>
public enum UiThreadGuardMode
{
    /// <summary>No checks (unit tests and production builds without <c>OGMA_E2E</c>).</summary>
    Off = 0,

    /// <summary>Log each violation at Error (Release builds run with <c>OGMA_E2E=1</c>).</summary>
    Log = 1,

    /// <summary>Log and throw (Debug builds of the running app).</summary>
    Throw = 2,
}

/// <summary>
/// The Sept-23 Phase 02 UI-thread discipline guard (T02.5, K72). View models call
/// <see cref="Verify"/> from <c>OnPropertyChanged</c>; when a bound property is raised from a
/// thread-pool thread the guard logs <c>ui.thread.violation</c> (and throws in Debug). It only
/// checks notifications that have subscribers, because an unbound view model is not yet visible
/// and may legitimately be built on a background thread.
/// </summary>
public static class UiThreadGuard
{
    private static Func<bool> _checkAccess = static () => true;
    private static ILogger _logger = NullLogger.Instance;

    /// <summary>The active mode.</summary>
    public static UiThreadGuardMode Mode { get; private set; }

    /// <summary>The number of violations seen since the guard was configured.</summary>
    public static int ViolationCount => _violationCount;

    private static int _violationCount;

    /// <summary>Enables or disables the guard.</summary>
    /// <param name="mode">The guard mode.</param>
    /// <param name="checkAccess">Returns whether the caller is on the UI thread.</param>
    /// <param name="logger">The logger for violations.</param>
    public static void Configure(UiThreadGuardMode mode, Func<bool> checkAccess, ILogger logger)
    {
        _checkAccess = checkAccess ?? throw new ArgumentNullException(nameof(checkAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Interlocked.Exchange(ref _violationCount, 0);
        Mode = mode;
    }

    /// <summary>Returns the guard mode for this build and environment.</summary>
    /// <param name="readEnvironmentVariable">Environment reader (for tests).</param>
    /// <returns><see cref="UiThreadGuardMode.Throw"/> in Debug, <see cref="UiThreadGuardMode.Log"/>
    /// with <c>OGMA_E2E=1</c>, otherwise <see cref="UiThreadGuardMode.Off"/>.</returns>
    public static UiThreadGuardMode ResolveMode(Func<string, string?>? readEnvironmentVariable = null)
    {
#if DEBUG
        return UiThreadGuardMode.Throw;
#else
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        return string.Equals(readEnvironmentVariable("OGMA_E2E"), "1", StringComparison.Ordinal)
            ? UiThreadGuardMode.Log
            : UiThreadGuardMode.Off;
#endif
    }

    /// <summary>Checks that a property-change notification is raised on the UI thread.</summary>
    /// <param name="owner">The view model raising the notification.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="handler">The current subscribers; <see langword="null"/> skips the check.</param>
    public static void Verify(object owner, string? propertyName, PropertyChangedEventHandler? handler)
    {
        if (Mode == UiThreadGuardMode.Off || handler is null || _checkAccess())
        {
            return;
        }

        Interlocked.Increment(ref _violationCount);
        string ownerName = owner?.GetType().Name ?? "unknown";
        AppLog.UiThreadViolation(_logger, ownerName, propertyName ?? "*");
        if (Mode == UiThreadGuardMode.Throw)
        {
            throw new InvalidOperationException(
                $"{ownerName}.{propertyName} was changed off the UI thread. Marshal it with IUiDispatcher.");
        }
    }
}
