using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.Navigation;

/// <summary>
/// In-memory <see cref="INavigationState"/> with a bounded back/forward history
/// (Sept-23 Phase 07, T07.2). Not thread-safe: it is owned by the UI thread.
/// </summary>
public sealed class NavigationState : INavigationState
{
    /// <summary>The maximum number of history entries kept.</summary>
    public const int MaxHistory = 50;

    private readonly List<Entry> _entries;
    private int _index;

    /// <summary>Initializes a new instance of the <see cref="NavigationState"/> class at the Library route.</summary>
    public NavigationState()
        : this(NavigationRoute.Library)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationState"/> class.</summary>
    /// <param name="initial">The initial route.</param>
    public NavigationState(NavigationRoute initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _entries = [new Entry(initial)];
    }

    /// <inheritdoc />
    public event EventHandler<NavigationChangedEventArgs>? Changed;

    /// <inheritdoc />
    public NavigationRoute Current => _entries[_index].Route;

    /// <inheritdoc />
    public bool CanGoBack => _index > 0;

    /// <inheritdoc />
    public bool CanGoForward => _index < _entries.Count - 1;

    /// <inheritdoc />
    public Func<double>? ScrollOffsetProvider { get; set; }

    /// <inheritdoc />
    public bool Navigate(NavigationRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route == Current)
        {
            return false;
        }

        NavigationRoute previous = Current;
        RecordScroll();
        _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(new Entry(route));
        if (_entries.Count > MaxHistory)
        {
            _entries.RemoveAt(0);
        }

        _index = _entries.Count - 1;
        Changed?.Invoke(this, new NavigationChangedEventArgs(previous, route, 0, isHistoryMove: false));
        return true;
    }

    /// <inheritdoc />
    public void Replace(NavigationRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        NavigationRoute previous = Current;
        if (route == previous)
        {
            return;
        }

        _entries[_index] = new Entry(route);
        Changed?.Invoke(this, new NavigationChangedEventArgs(previous, route, 0, isHistoryMove: false));
    }

    /// <inheritdoc />
    public bool GoBack() => Move(-1);

    /// <inheritdoc />
    public bool GoForward() => Move(1);

    private bool Move(int delta)
    {
        int target = _index + delta;
        if (target < 0 || target >= _entries.Count)
        {
            return false;
        }

        NavigationRoute previous = Current;
        RecordScroll();
        _index = target;
        Entry entry = _entries[_index];
        Changed?.Invoke(this, new NavigationChangedEventArgs(previous, entry.Route, entry.ScrollOffset, isHistoryMove: true));
        return true;
    }

    private void RecordScroll()
    {
        if (ScrollOffsetProvider is { } provider)
        {
            _entries[_index].ScrollOffset = provider();
        }
    }

    private sealed class Entry(NavigationRoute route)
    {
        public NavigationRoute Route { get; } = route;

        public double ScrollOffset { get; set; }
    }
}
