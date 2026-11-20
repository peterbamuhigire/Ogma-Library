using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Workers;

/// <summary>
/// Turns the existing reader session events into a "the user is reading right now" signal so
/// background embedding can yield the CPU (Sept-23 Phase 14, slice 4). A session counts as
/// active while it is open and the reader has been used within <see cref="QuietPeriod"/>.
/// </summary>
internal sealed class ReaderActivityTracker : IObserver<ReaderEvent>, IDisposable
{
    /// <summary>How long after the last page turn, scroll or zoom the reader still counts as active.</summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(5);

    private readonly Func<DateTimeOffset> _now;
    private readonly IDisposable _subscription;
    private readonly Lock _sync = new();
    private bool _open;
    private DateTimeOffset _lastActivity = DateTimeOffset.MinValue;

    public ReaderActivityTracker(IReaderSessionReadModel reader, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _subscription = reader.Events.Subscribe(this);
    }

    /// <summary>True while an open reader session has been used within <see cref="QuietPeriod"/>.</summary>
    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return _open && _now() - _lastActivity < QuietPeriod;
            }
        }
    }

    public void OnNext(ReaderEvent value)
    {
        lock (_sync)
        {
            switch (value)
            {
                case ReaderEvent.SessionClosed:
                    _open = false;
                    break;
                case ReaderEvent.SessionOpened:
                case ReaderEvent.PageChanged:
                case ReaderEvent.Scrolled:
                case ReaderEvent.ZoomChanged:
                case ReaderEvent.DisplayModeChanged:
                    _open = true;
                    _lastActivity = _now();
                    break;
            }
        }
    }

    public void OnError(Exception error)
    {
        // A failed reader stream must not stall indexing: treat the reader as idle.
        lock (_sync)
        {
            _open = false;
        }
    }

    public void OnCompleted() => OnError(new InvalidOperationException("Reader events completed."));

    public void Dispose() => _subscription.Dispose();
}
