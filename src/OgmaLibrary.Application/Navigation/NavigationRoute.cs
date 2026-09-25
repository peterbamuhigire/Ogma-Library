namespace OgmaLibrary.Application.Navigation;

/// <summary>
/// The kinds of routed surfaces in the shell (Sept-23 Phase 07, T07.2). Exactly one route is
/// current at a time, so destinations can never stack (K12).
/// </summary>
public enum RouteKind
{
    /// <summary>The catalogue (grid, list or directory).</summary>
    Library = 0,

    /// <summary>Global search.</summary>
    Search = 1,

    /// <summary>The PDF reader for one book.</summary>
    Reader = 2,

    /// <summary>The two-session split reader.</summary>
    SplitView = 3,

    /// <summary>The reading advisor.</summary>
    Advisor = 4,

    /// <summary>The reading plan, owned by the Advisor destination.</summary>
    ReadingPlan = 5,

    /// <summary>User collections (shelves).</summary>
    Collections = 6,

    /// <summary>Index and background activity.</summary>
    Activity = 7,

    /// <summary>Settings, optionally opened at a section.</summary>
    Settings = 8,

    /// <summary>Classroom sharing and smart search (only when the capability is on).</summary>
    Classroom = 9,

    /// <summary>The 3D bookshelf view of the library.</summary>
    Shelf3D = 10,
}

/// <summary>The top-level destinations shown in the navigation rail.</summary>
public enum ShellDestination
{
    /// <summary>Library (catalogue and 3D shelf).</summary>
    Library = 0,

    /// <summary>Search.</summary>
    Search = 1,

    /// <summary>Reading (reader and split reader).</summary>
    Reading = 2,

    /// <summary>Advisor (advisor and reading plan).</summary>
    Advisor = 3,

    /// <summary>Collections.</summary>
    Collections = 4,

    /// <summary>Activity.</summary>
    Activity = 5,

    /// <summary>Settings.</summary>
    Settings = 6,

    /// <summary>Classroom.</summary>
    Classroom = 7,
}

/// <summary>An immutable shell route with its optional parameters.</summary>
/// <param name="Kind">The routed surface.</param>
/// <param name="BookId">The book shown by a reader route.</param>
/// <param name="Page">The zero-based page hint of a reader route.</param>
/// <param name="Section">The settings or classroom section.</param>
public sealed record NavigationRoute(
    RouteKind Kind,
    string? BookId = null,
    int? Page = null,
    string? Section = null)
{
    /// <summary>The Library route.</summary>
    public static NavigationRoute Library { get; } = new(RouteKind.Library);

    /// <summary>The rail destination that owns this route.</summary>
    public ShellDestination Destination => Kind switch
    {
        RouteKind.Library or RouteKind.Shelf3D => ShellDestination.Library,
        RouteKind.Search => ShellDestination.Search,
        RouteKind.Reader or RouteKind.SplitView => ShellDestination.Reading,
        RouteKind.Advisor or RouteKind.ReadingPlan => ShellDestination.Advisor,
        RouteKind.Collections => ShellDestination.Collections,
        RouteKind.Activity => ShellDestination.Activity,
        RouteKind.Settings => ShellDestination.Settings,
        RouteKind.Classroom => ShellDestination.Classroom,
        _ => ShellDestination.Library,
    };

    /// <summary>Creates a reader route.</summary>
    /// <param name="bookId">The book to read, or <see langword="null"/> for the last open book.</param>
    /// <param name="page">The zero-based page hint.</param>
    /// <returns>The route.</returns>
    public static NavigationRoute Reader(string? bookId, int? page = null) =>
        new(RouteKind.Reader, bookId, page);

    /// <summary>Creates a settings route opened at a section.</summary>
    /// <param name="section">The section identifier, or <see langword="null"/> for the overview.</param>
    /// <returns>The route.</returns>
    public static NavigationRoute Settings(string? section = null) =>
        new(RouteKind.Settings, Section: section);
}

/// <summary>Describes a change of the current route.</summary>
public sealed class NavigationChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="NavigationChangedEventArgs"/> class.</summary>
    /// <param name="previous">The route that was current before the change.</param>
    /// <param name="current">The new current route.</param>
    /// <param name="restoredScrollOffset">The scroll offset recorded for the new route (0 for a new entry).</param>
    /// <param name="isHistoryMove">Whether the change came from Back or Forward.</param>
    public NavigationChangedEventArgs(
        NavigationRoute previous,
        NavigationRoute current,
        double restoredScrollOffset,
        bool isHistoryMove)
    {
        Previous = previous;
        Current = current;
        RestoredScrollOffset = restoredScrollOffset;
        IsHistoryMove = isHistoryMove;
    }

    /// <summary>The route that was current before the change.</summary>
    public NavigationRoute Previous { get; }

    /// <summary>The new current route.</summary>
    public NavigationRoute Current { get; }

    /// <summary>The scroll offset recorded for the new route (0 for a new entry).</summary>
    public double RestoredScrollOffset { get; }

    /// <summary>Whether the change came from Back or Forward.</summary>
    public bool IsHistoryMove { get; }
}

/// <summary>
/// The single source of truth for which shell surface is shown, with a bounded back/forward
/// history (Sept-23 Phase 07, T07.2). Implementations live in the app layer.
/// </summary>
public interface INavigationState
{
    /// <summary>The current route.</summary>
    NavigationRoute Current { get; }

    /// <summary>Whether <see cref="GoBack"/> would move.</summary>
    bool CanGoBack { get; }

    /// <summary>Whether <see cref="GoForward"/> would move.</summary>
    bool CanGoForward { get; }

    /// <summary>Raised after the current route changes.</summary>
    event EventHandler<NavigationChangedEventArgs>? Changed;

    /// <summary>
    /// Supplies the scroll offset of the current surface, recorded into history when the user
    /// leaves it so Back can restore it.
    /// </summary>
    Func<double>? ScrollOffsetProvider { get; set; }

    /// <summary>Makes <paramref name="route"/> current; a route equal to the current one is ignored.</summary>
    /// <param name="route">The route to show.</param>
    /// <returns><see langword="true"/> when the current route changed.</returns>
    bool Navigate(NavigationRoute route);

    /// <summary>Replaces the current route without adding a history entry.</summary>
    /// <param name="route">The route to show.</param>
    void Replace(NavigationRoute route);

    /// <summary>Moves one entry back.</summary>
    /// <returns><see langword="true"/> when the current route changed.</returns>
    bool GoBack();

    /// <summary>Moves one entry forward.</summary>
    /// <returns><see langword="true"/> when the current route changed.</returns>
    bool GoForward();
}
