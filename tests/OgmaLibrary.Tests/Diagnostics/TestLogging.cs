using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.Tests.Diagnostics;

/// <summary>One captured log entry.</summary>
public sealed record CapturedLogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

/// <summary>An in-memory logger for asserting Phase 02 log events.</summary>
public sealed class CapturingLogger : ILogger
{
    private readonly List<CapturedLogEntry> _entries = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<CapturedLogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), exception));
        }
    }
}

/// <summary>An in-memory notifier for asserting Phase 02 user notifications.</summary>
public sealed class CapturingNotifier : IUserNotifier
{
    private readonly List<UserNotification> _notifications = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<UserNotification> Notifications
    {
        get
        {
            lock (_gate)
            {
                return [.. _notifications];
            }
        }
    }

    public void Notify(UserNotification notification)
    {
        lock (_gate)
        {
            _notifications.Add(notification);
        }
    }
}

/// <summary>A clock fixed at a settable instant.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
