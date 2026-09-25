namespace OgmaLibrary.Application.Diagnostics;

/// <summary>
/// Shared rules for deciding which exceptions a safety net may absorb (Sept-23 Phase 02).
/// </summary>
public static class ExceptionClassification
{
    /// <summary>
    /// Returns <see langword="true"/> for process-corrupting failures that must never be
    /// swallowed: the process state cannot be trusted after them.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>Whether the exception is fatal.</returns>
    public static bool IsFatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is OutOfMemoryException or
            StackOverflowException or
            AccessViolationException;
    }
}
