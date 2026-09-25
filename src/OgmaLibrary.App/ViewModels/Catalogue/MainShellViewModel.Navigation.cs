using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using OgmaLibrary.App.Icons;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.Navigation;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>A transient tool owned by the Library destination; at most one is open.</summary>
public enum LibraryDrawer
{
    /// <summary>No drawer is open.</summary>
    None = 0,

    /// <summary>Title and author filters.</summary>
    Filter = 1,

    /// <summary>Library folders and files that need attention.</summary>
    Folders = 2,

    /// <summary>Relocation reviews for moved files.</summary>
    Reconciliation = 3,
}

/// <summary>A removable chip that makes an active catalogue filter visible (K12).</summary>
/// <param name="Id">The filter dimension.</param>
/// <param name="Label">The visible chip text.</param>
/// <param name="RemoveName">The accessible name of the chip's remove button.</param>
public sealed record FilterChip(string Id, string Label, string RemoveName);

/// <summary>A capability row on the Settings overview.</summary>
/// <param name="Name">The feature name.</param>
/// <param name="Status">Available or not set up.</param>
/// <param name="Detail">What the state means for the user.</param>
/// <param name="IsAvailable">Whether the capability is available.</param>
public sealed record CapabilityRow(string Name, string Status, string Detail, bool IsAvailable);

/// <summary>A keyboard shortcut row for the shortcuts sheet.</summary>
/// <param name="Label">What the shortcut does.</param>
/// <param name="Gesture">The key combination.</param>
public sealed record ShortcutRow(string Label, string Gesture);

/// <summary>One destination in the navigation rail (Sept-23 Phase 07).</summary>
public sealed class RailItemViewModel : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private string _toolTip = string.Empty;
    private string _status = string.Empty;
    private bool _isSelected;
    private bool _isVisible = true;

    /// <summary>Initializes a new instance of the <see cref="RailItemViewModel"/> class.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="automationId">The stable UI Automation id.</param>
    /// <param name="iconKey">The icon catalogue key.</param>
    public RailItemViewModel(ShellDestination destination, string automationId, string iconKey)
    {
        Destination = destination;
        AutomationId = automationId;
        IconPath = IconCatalog.GetAvaresPath(iconKey) ?? string.Empty;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The destination.</summary>
    public ShellDestination Destination { get; }

    /// <summary>The stable UI Automation id.</summary>
    public string AutomationId { get; }

    /// <summary>The icon path.</summary>
    public string IconPath { get; }

    /// <summary>The visible label.</summary>
    public string Label { get => _label; set => Set(ref _label, value); }

    /// <summary>The tooltip, including the shortcut.</summary>
    public string ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }

    /// <summary>The UIA item status ("Current page" when selected).</summary>
    public string Status { get => _status; set => Set(ref _status, value); }

    /// <summary>Whether this is the current destination.</summary>
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    /// <summary>Whether the destination's capability is available.</summary>
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            UiThreadGuard.Verify(this, name, PropertyChanged);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

/// <summary>
/// Routed navigation, drawers, capabilities, the command registry and the responsive shell
/// state (Sept-23 Phase 07: K11, K12, K17, UX-003, UX-005).
/// </summary>
public sealed partial class MainShellViewModel
{
    /// <summary>Below this shell width the rail collapses to icons.</summary>
    public const double RailCompactBelow = 1100;

    /// <summary>Below this shell width the inspector overlays the content.</summary>
    public const double InspectorOverlayBelow = 1280;

    private static readonly bool IsMac = OperatingSystem.IsMacOS();

    private NavigationState _navigation = null!;
    private ICapabilityState _capabilities = null!;
    private CommandRegistry _commands = null!;
    private LibraryDrawer _activeDrawer;
    private bool? _railCompactPreference;
    private double _shellWidth = 1280;
    private string? _lastReaderBookId;
    private bool _isShortcutSheetOpen;
    private bool _suppressClassroomRefresh;

    /// <summary>The rail destinations in display order (Settings is last).</summary>
    public ObservableCollection<RailItemViewModel> RailItems { get; } = [];

    /// <summary>The routed navigation state.</summary>
    public INavigationState Navigation => _navigation;

    /// <summary>The capability state that rail items and actions bind to.</summary>
    public ICapabilityState Capabilities => _capabilities;

    /// <summary>The command registry backing the palette, menus and shortcuts.</summary>
    public CommandRegistry Commands => _commands;

    /// <summary>The current route.</summary>
    public NavigationRoute CurrentRoute => _navigation.Current;

    /// <summary>The current rail destination.</summary>
    public ShellDestination CurrentDestination => _navigation.Current.Destination;

    /// <summary>Optional diagnostics export, supplied by the startup shell.</summary>
    public Func<Task>? ExportDiagnostics { get; set; }

    // ── Route-derived state ───────────────────────────────────────────────────

    /// <summary>The currently active content area, derived from the route.</summary>
    public ShellView ActiveView => CurrentRoute.Kind switch
    {
        RouteKind.Library => ShellView.Catalogue,
        RouteKind.Reader => ShellView.Reader,
        RouteKind.SplitView => ShellView.SplitView,
        RouteKind.Classroom => IsClassroomSmartSearchSection ? ShellView.StudentSmartSearch : ShellView.SharingSettings,
        RouteKind.Advisor => ShellView.Advisor,
        RouteKind.ReadingPlan => ShellView.ReadingPlan,
        RouteKind.Shelf3D => ShellView.Bookshelf3D,
        RouteKind.Search => ShellView.Search,
        RouteKind.Collections => ShellView.Collections,
        RouteKind.Activity => ShellView.Activity,
        RouteKind.Settings => ShellView.Settings,
        _ => ShellView.Catalogue,
    };

    /// <summary>True when the catalogue view is the active content area.</summary>
    public bool IsCatalogueActive => CurrentRoute.Kind == RouteKind.Library;

    /// <summary>True when any Library surface (catalogue or 3D shelf) is shown.</summary>
    public bool IsLibraryDestination => CurrentDestination == ShellDestination.Library;

    /// <summary>Whether the catalogue pager is shown: only on the catalogue route with more than one page.</summary>
    public bool IsPagerVisible => IsCatalogueActive && Catalogue.TotalPages > 1;

    /// <summary>True when the reader view is the active content area.</summary>
    public bool IsReaderActive => CurrentRoute.Kind == RouteKind.Reader;

    /// <summary>True when the Reading destination shows an open book.</summary>
    public bool IsReaderContentVisible => IsReaderActive && Reader is { IsOpen: true };

    /// <summary>True when the Reading destination has no open book.</summary>
    public bool IsReadingEmptyVisible => IsReaderActive && Reader is not { IsOpen: true };

    /// <summary>True when the split reader is the active content area.</summary>
    public bool IsSplitViewActive => CurrentRoute.Kind == RouteKind.SplitView;

    /// <summary>True when the Reading destination is current.</summary>
    public bool IsReadingDestination => CurrentDestination == ShellDestination.Reading;

    /// <summary>True when the Sharing settings surface is the active content area.</summary>
    public bool IsSharingSettingsActive => ActiveView == ShellView.SharingSettings;

    /// <summary>True when the student AI smart-search surface is the active content area.</summary>
    public bool IsStudentSmartSearchActive => ActiveView == ShellView.StudentSmartSearch;

    /// <summary>True when the Classroom destination is current.</summary>
    public bool IsClassroomActive => CurrentRoute.Kind == RouteKind.Classroom;

    /// <summary>True when the recommendation advisor route is active.</summary>
    public bool IsAdvisorActive => CurrentRoute.Kind == RouteKind.Advisor;

    /// <summary>True when the reading-plan route is active.</summary>
    public bool IsReadingPlanActive => CurrentRoute.Kind == RouteKind.ReadingPlan;

    /// <summary>True when the Advisor destination (advisor or reading plan) is current.</summary>
    public bool IsAdvisorDestination => CurrentDestination == ShellDestination.Advisor;

    /// <summary>True when the 3D bookshelf route is active.</summary>
    public bool IsBookshelf3DActive => CurrentRoute.Kind == RouteKind.Shelf3D;

    /// <summary>True when the Search destination is current.</summary>
    public bool IsSearchActive => CurrentRoute.Kind == RouteKind.Search;

    /// <summary>True when the Collections destination is current.</summary>
    public bool IsCollectionsActive => CurrentRoute.Kind == RouteKind.Collections;

    /// <summary>True when the Activity destination is current.</summary>
    public bool IsActivityActive => CurrentRoute.Kind == RouteKind.Activity;

    /// <summary>True when the Settings destination is current.</summary>
    public bool IsSettingsActive => CurrentRoute.Kind == RouteKind.Settings;

    /// <summary>Whether the search destination is shown (compatibility name).</summary>
    public bool IsSearchPanelOpen
    {
        get => IsSearchActive;
        set
        {
            if (value)
            {
                NavigateTo(new NavigationRoute(RouteKind.Search));
            }
            else if (IsSearchActive)
            {
                LeaveTransientDestination();
            }
        }
    }

    /// <summary>Whether the Activity destination (Index Manager) is shown (compatibility name).</summary>
    public bool IsIndexManagerOpen
    {
        get => IsActivityActive;
        set
        {
            if (value)
            {
                NavigateTo(new NavigationRoute(RouteKind.Activity));
            }
            else if (IsActivityActive)
            {
                LeaveTransientDestination();
            }
        }
    }

    // ── Drawers ───────────────────────────────────────────────────────────────

    /// <summary>The open Library drawer; opening one closes the others and navigation closes all.</summary>
    public LibraryDrawer ActiveDrawer
    {
        get => _activeDrawer;
        set
        {
            if (_activeDrawer == value)
            {
                return;
            }

            _activeDrawer = value;
            if (value != LibraryDrawer.None && !IsLibraryDestination)
            {
                NavigateTo(NavigationRoute.Library);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFilterPanelOpen));
            OnPropertyChanged(nameof(IsFoldersDrawerOpen));
            OnPropertyChanged(nameof(IsReconciliationReviewPanelOpen));
            OnPropertyChanged(nameof(IsDrawerOpen));
            OnPropertyChanged(nameof(DrawerTitle));
            OnPropertyChanged(nameof(IsDrawerHeaderVisible));
        }
    }

    /// <summary>Whether any drawer is open.</summary>
    public bool IsDrawerOpen => _activeDrawer != LibraryDrawer.None;

    /// <summary>Whether the filter drawer is open.</summary>
    public bool IsFilterPanelOpen
    {
        get => _activeDrawer == LibraryDrawer.Filter;
        set => SetDrawer(LibraryDrawer.Filter, value);
    }

    /// <summary>Whether the Library folders drawer is open.</summary>
    public bool IsFoldersDrawerOpen
    {
        get => _activeDrawer == LibraryDrawer.Folders;
        set => SetDrawer(LibraryDrawer.Folders, value);
    }

    /// <summary>Whether the relocation-review drawer is open.</summary>
    public bool IsReconciliationReviewPanelOpen => _activeDrawer == LibraryDrawer.Reconciliation;

    /// <summary>Toggles the filter drawer.</summary>
    public void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    /// <summary>Toggles the Library folders drawer.</summary>
    public void ToggleFoldersDrawer() => IsFoldersDrawerOpen = !IsFoldersDrawerOpen;

    /// <summary>Toggles the search destination.</summary>
    public void ToggleSearchPanel() => IsSearchPanelOpen = !IsSearchPanelOpen;

    /// <summary>Opens the Activity destination and loads the index state.</summary>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>A task that completes when the load finishes.</returns>
    public Task ToggleIndexManagerAsync(CancellationToken cancellationToken = default)
    {
        IsIndexManagerOpen = !IsIndexManagerOpen;
        return Task.CompletedTask;
    }

    /// <summary>Loads and toggles the relocation-review drawer.</summary>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>A task that completes when the reviews are loaded.</returns>
    public async Task ToggleReconciliationReviewsAsync(CancellationToken cancellationToken = default)
    {
        bool open = !IsReconciliationReviewPanelOpen;
        SetDrawer(LibraryDrawer.Reconciliation, open);
        if (open && ReconciliationReviews is not null)
        {
            await ReconciliationReviews.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Closes the relocation-review drawer.</summary>
    public void CloseReconciliationReviews() => SetDrawer(LibraryDrawer.Reconciliation, false);

    /// <summary>Closes the open drawer.</summary>
    /// <returns><see langword="true"/> when a drawer was open.</returns>
    public bool CloseDrawer()
    {
        if (_activeDrawer == LibraryDrawer.None)
        {
            return false;
        }

        ActiveDrawer = LibraryDrawer.None;
        return true;
    }

    // ── Filter chips (K12) ────────────────────────────────────────────────────

    /// <summary>Chips for every active catalogue filter, so a hidden filter can never silently apply.</summary>
    public IReadOnlyList<FilterChip> ActiveFilterChips
    {
        get
        {
            var chips = new List<FilterChip>();
            CatalogueFilterViewModel filter = Catalogue.Filter;
            CultureInfo culture = CultureInfo.CurrentCulture;
            void Add(string id, string label) => chips.Add(new FilterChip(
                id,
                label,
                string.Format(culture, _localization["Filter.Chip.RemoveFormat"], label)));

            if (!string.IsNullOrWhiteSpace(filter.TitleSearch))
            {
                Add("title", string.Format(culture, _localization["Filter.Chip.TitleFormat"], filter.TitleSearch.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(filter.AuthorSearch))
            {
                Add("author", string.Format(culture, _localization["Filter.Chip.AuthorFormat"], filter.AuthorSearch.Trim()));
            }

            if (filter.SelectedShelfId is { } shelfId)
            {
                string name = ShelfSidebar.Shelves.FirstOrDefault(shelf => shelf.ShelfId == shelfId)?.Name ?? shelfId;
                Add("shelf", string.Format(culture, _localization["Filter.Chip.CollectionFormat"], name));
            }

            if (filter.StatusFilter.HasValue)
            {
                Add("status", _localization["Filter.Chip.Status"]);
            }

            if (filter.MinRating.HasValue || filter.MaxRating.HasValue)
            {
                Add("rating", _localization["Filter.Chip.Rating"]);
            }

            if (filter.AvailabilityFilter.HasValue)
            {
                Add("availability", _localization["Filter.Chip.Availability"]);
            }

            return chips;
        }
    }

    /// <summary>Whether the chip row is shown (Library catalogue with at least one active filter).</summary>
    public bool HasActiveFilterChips => IsCatalogueActive && Catalogue.Filter.HasActiveFilters;

    /// <summary>Removes one filter dimension.</summary>
    /// <param name="chipId">The chip identifier.</param>
    public void RemoveFilter(string chipId)
    {
        CatalogueFilterViewModel filter = Catalogue.Filter;
        switch (chipId)
        {
            case "title":
                filter.TitleSearch = null;
                break;
            case "author":
                filter.AuthorSearch = null;
                break;
            case "shelf":
                filter.SelectedShelfId = null;
                break;
            case "status":
                filter.StatusFilter = null;
                break;
            case "rating":
                filter.MinRating = null;
                filter.MaxRating = null;
                break;
            case "availability":
                filter.AvailabilityFilter = null;
                break;
        }
    }

    /// <summary>Shows the books of the selected collection in the Library.</summary>
    public void ShowSelectedCollectionInLibrary()
    {
        if (ShelfSidebar.SelectedShelf is { } shelf)
        {
            Catalogue.Filter.SelectedShelfId = shelf.ShelfId;
            NavigateTo(NavigationRoute.Library);
        }
    }

    /// <summary>Whether a collection is selected.</summary>
    public bool CanShowSelectedCollection => ShelfSidebar.SelectedShelf is not null;

    // ── Responsive shell ──────────────────────────────────────────────────────

    /// <summary>Whether the rail shows icons only.</summary>
    public bool IsRailCompact => _railCompactPreference ?? _shellWidth < RailCompactBelow;

    /// <summary>Whether the rail shows labels.</summary>
    public bool IsRailExpanded => !IsRailCompact;

    /// <summary>Whether the book inspector overlays the content instead of taking a column.</summary>
    public bool IsInspectorOverlay => _shellWidth < InspectorOverlayBelow;

    /// <summary>Records the shell width so the rail and inspector can adapt.</summary>
    /// <param name="width">The width in device-independent pixels.</param>
    public void UpdateShellWidth(double width)
    {
        if (double.IsNaN(width) || width <= 0 || Math.Abs(width - _shellWidth) < 0.5)
        {
            return;
        }

        bool compact = IsRailCompact;
        bool overlay = IsInspectorOverlay;
        _shellWidth = width;
        if (compact != IsRailCompact)
        {
            RaiseRailChanged();
        }

        if (overlay != IsInspectorOverlay)
        {
            OnPropertyChanged(nameof(IsInspectorOverlay));
        }
    }

    /// <summary>Collapses or expands the rail, overriding the automatic width rule.</summary>
    public void ToggleRail()
    {
        _railCompactPreference = !IsRailCompact;
        RaiseRailChanged();
    }

    /// <summary>The accessible name of the rail collapse toggle.</summary>
    public string RailToggleLabel => IsRailCompact ? _localization["Navigation.ExpandRail"] : _localization["Navigation.CollapseRail"];

    /// <summary>The glyph of the rail collapse toggle.</summary>
    public string RailToggleGlyph => IsRailCompact ? "»" : "«";

    /// <summary>The filter icon.</summary>
    public string FilterIconPath { get; } = IconCatalog.GetAvaresPath("ic_cat_filter") ?? string.Empty;

    /// <summary>Whether the grid view is the selected Library view.</summary>
    public bool IsGridViewSelected => IsCatalogueActive && Catalogue.IsGridView;

    /// <summary>Whether the list view is the selected Library view.</summary>
    public bool IsListViewSelected => IsCatalogueActive && Catalogue.IsListView;

    /// <summary>Whether the directory view is the selected Library view.</summary>
    public bool IsDirectoryViewSelected => IsCatalogueActive && Catalogue.IsDirectoryView;

    /// <summary>Whether the Classroom tabs are shown.</summary>
    public bool IsClassroomTabsVisible => IsClassroomActive && HasClassroomTabs;

    /// <summary>The title of the open drawer.</summary>
    public string DrawerTitle => _activeDrawer switch
    {
        LibraryDrawer.Filter => _localization["Catalogue.Filter.Filters"],
        LibraryDrawer.Folders => _localization["Library.Toolbar.Folders"],
        LibraryDrawer.Reconciliation => _localization["Reconciliation.Review.Title"],
        _ => string.Empty,
    };

    /// <summary>Whether the shared drawer header is shown (the relocation panel has its own).</summary>
    public bool IsDrawerHeaderVisible => _activeDrawer is LibraryDrawer.Filter or LibraryDrawer.Folders;

    /// <summary>The accessible name of the rail landmark.</summary>
    public string RailName => _localization["Navigation.Rail"];

    // ── Capability-driven visibility (K17) ────────────────────────────────────

    /// <summary>Whether the 3D shelf view is offered.</summary>
    public bool IsShelf3DAvailable => Bookshelf3D is not null && _capabilities.IsShelf3DAvailable;

    /// <summary>True when the student smart-search route is available (connected classroom client only).</summary>
    public bool IsStudentSmartSearchVisible => StudentSmartSearch is not null && _capabilities.IsClassroomClientConnected;

    /// <summary>True when the Host sharing surface is available.</summary>
    public bool IsHostSharingVisible => HostSharing is not null && _capabilities.IsClassroomHostEnabled;

    /// <summary>Whether the Classroom destination is available.</summary>
    public bool IsClassroomAvailable => IsHostSharingVisible || IsStudentSmartSearchVisible;

    /// <summary>Whether both classroom surfaces exist, so the Classroom toolbar shows tabs.</summary>
    public bool HasClassroomTabs => IsHostSharingVisible && IsStudentSmartSearchVisible;

    /// <summary>Whether the Advisor shows its "set up AI" explanation instead of failing (G7).</summary>
    public bool IsAdvisorSetupNeeded => !_capabilities.IsAiConfigured;

    private bool IsClassroomSmartSearchSection =>
        !IsHostSharingVisible || string.Equals(CurrentRoute.Section, "smart-search", StringComparison.Ordinal);

    /// <summary>The Settings overview's capability list.</summary>
    public IReadOnlyList<CapabilityRow> CapabilityRows =>
    [
        Row("Settings.Capability.Ai", _capabilities.IsAiConfigured),
        Row("Settings.Capability.Classroom", _capabilities.IsClassroomAvailable),
        Row("Settings.Capability.Shelf3D", _capabilities.IsShelf3DAvailable),
        Row("Settings.Capability.Metadata", _capabilities.AreMetadataProvidersEnabled),
    ];

    // ── Labels ────────────────────────────────────────────────────────────────

    /// <summary>The heading of the current destination.</summary>
    public string DestinationHeading => CurrentRoute.Kind switch
    {
        RouteKind.Search => _localization["Navigation.Search"],
        RouteKind.Reader or RouteKind.SplitView => _localization["Navigation.Reading"],
        RouteKind.Advisor or RouteKind.ReadingPlan => _localization["Navigation.Advisor"],
        RouteKind.Collections => _localization["Navigation.Collections"],
        RouteKind.Activity => _localization["Navigation.Activity"],
        RouteKind.Settings => _localization["Navigation.Settings"],
        RouteKind.Classroom => _localization["Navigation.Classroom"],
        _ => _localization["Navigation.Library"],
    };

    /// <summary>Label of the add-folder toolbar action.</summary>
    public string AddFolderLabel => _localization["Library.Toolbar.AddFolder"];

    /// <summary>Label of the folders drawer toggle.</summary>
    public string FoldersLabel => _localization["Library.Toolbar.Folders"];

    /// <summary>Accessible name of the view switcher group.</summary>
    public string ViewSwitcherLabel => _localization["Library.Toolbar.View"];

    /// <summary>Accessible name of the chip row.</summary>
    public string FilterChipsLabel => _localization["Filter.Chips.Name"];

    /// <summary>Label of drawer close buttons.</summary>
    public string CloseDrawerLabel => _localization["Icon.ic_close_panel.Label"];

    /// <summary>Label of the Ask tab of the Advisor destination.</summary>
    public string AdvisorAskLabel => _localization["Advisor.Tab.Ask"];

    /// <summary>Advisor setup heading.</summary>
    public string AdvisorSetupHeading => _localization["Advisor.Unconfigured.Heading"];

    /// <summary>Advisor setup body.</summary>
    public string AdvisorSetupBody => _localization["Advisor.Unconfigured.Body"];

    /// <summary>Advisor setup action.</summary>
    public string AdvisorSetupAction => _localization["Advisor.Unconfigured.Action"];

    /// <summary>Reading empty-state heading.</summary>
    public string ReadingEmptyHeading => _localization["Reading.Empty.Heading"];

    /// <summary>Reading empty-state body.</summary>
    public string ReadingEmptyBody => _localization["Reading.Empty.Body"];

    /// <summary>Label of the reopen-last-book action.</summary>
    public string ReadingResumeLabel => _localization["Reading.Empty.Resume"];

    /// <summary>Whether a last book can be reopened.</summary>
    public bool CanResumeLastBook => _lastReaderBookId is not null;

    /// <summary>Label of the go-to-library action.</summary>
    public string GoToLibraryLabel => _localization["Reading.Empty.GoToLibrary"];

    /// <summary>Label of the show-collection action.</summary>
    public string ShowCollectionLabel => _localization["Collections.ShowInLibrary"];

    /// <summary>Hint shown on the Collections destination.</summary>
    public string CollectionsHint => _localization["Collections.Hint"];

    /// <summary>Settings overview introduction.</summary>
    public string SettingsIntro => _localization["Settings.Intro"];

    /// <summary>Settings capability section title.</summary>
    public string SettingsCapabilitiesTitle => _localization["Settings.Capabilities.Title"];

    /// <summary>Label of the Classroom smart-search tab.</summary>
    public string ClassroomSmartSearchLabel => _localization["Classroom.Tab.SmartSearch"];

    /// <summary>Title of the keyboard shortcuts sheet.</summary>
    public string ShortcutsTitle => _localization["Shortcuts.Title"];

    /// <summary>Label of close buttons on sheets.</summary>
    public string CloseLabel => _localization["Shortcuts.Close"];

    /// <summary>Label of the export-diagnostics action.</summary>
    public string ExportDiagnosticsLabel => _localization["Command.App.ExportDiagnostics"];

    /// <summary>Accessible name of the command palette close button.</summary>
    public string CommandPaletteCloseLabel => _localization["CommandPalette.Close"];

    /// <summary>The keyboard shortcut rows, generated from the registry.</summary>
    public IReadOnlyList<ShortcutRow> ShortcutRows =>
        _commands.All
            .Where(command => command.Gestures.Count > 0)
            .Select(command => new ShortcutRow(
                _localization[command.LabelKey],
                string.Join("  ", command.Gestures.Select(gesture => gesture.Format(IsMac)))))
            .ToList();

    /// <summary>Whether the keyboard shortcuts sheet is visible.</summary>
    public bool IsShortcutSheetOpen
    {
        get => _isShortcutSheetOpen;
        set
        {
            if (_isShortcutSheetOpen != value)
            {
                _isShortcutSheetOpen = value;
                OnPropertyChanged();
            }
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>Navigates to a route, redirecting unavailable capabilities honestly.</summary>
    /// <param name="route">The route.</param>
    /// <returns><see langword="true"/> when the route changed.</returns>
    public bool NavigateTo(NavigationRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        route = route.Kind switch
        {
            RouteKind.Classroom when !IsClassroomAvailable => NavigationRoute.Settings("classroom"),
            RouteKind.Shelf3D when !IsShelf3DAvailable => NavigationRoute.Library,
            RouteKind.Reader when route.BookId is null && Reader is { IsOpen: true, BookId: { } openId } =>
                NavigationRoute.Reader(openId),
            _ => route,
        };

        if (route.Kind == RouteKind.Reader && route.BookId is { } bookId)
        {
            _lastReaderBookId = bookId;
            OnPropertyChanged(nameof(CanResumeLastBook));
        }

        return _navigation.Navigate(route);
    }

    /// <summary>Navigates to a rail destination.</summary>
    /// <param name="destination">The destination.</param>
    public void NavigateTo(ShellDestination destination) => NavigateTo(destination switch
    {
        ShellDestination.Search => new NavigationRoute(RouteKind.Search),
        ShellDestination.Reading => NavigationRoute.Reader(null),
        ShellDestination.Advisor => new NavigationRoute(IsReadingPlanActive ? RouteKind.ReadingPlan : RouteKind.Advisor),
        ShellDestination.Collections => new NavigationRoute(RouteKind.Collections),
        ShellDestination.Activity => new NavigationRoute(RouteKind.Activity),
        ShellDestination.Settings => NavigationRoute.Settings(),
        ShellDestination.Classroom => new NavigationRoute(RouteKind.Classroom),
        _ => NavigationRoute.Library,
    });

    /// <summary>Goes back one route (Alt+Left, mouse button 4).</summary>
    /// <returns><see langword="true"/> when the route changed.</returns>
    public bool GoBack() => _navigation.GoBack();

    /// <summary>Goes forward one route (Alt+Right, mouse button 5).</summary>
    /// <returns><see langword="true"/> when the route changed.</returns>
    public bool GoForward() => _navigation.GoForward();

    /// <summary>Returns to the catalogue browsing surface.</summary>
    public void OpenCatalogue() => NavigateTo(NavigationRoute.Library);

    /// <summary>Opens the two-session split-reader route.</summary>
    public void OpenSplitView() => NavigateTo(new NavigationRoute(RouteKind.SplitView));

    /// <summary>Compatibility alias for callers using the former route name.</summary>
    public void OpenSplitViewScaffold() => OpenSplitView();

    /// <summary>Opens the Classroom sharing surface after refreshing the Host state.</summary>
    /// <param name="cancellationToken">A token to cancel the refresh.</param>
    /// <returns>A task that completes when the route is shown.</returns>
    public async Task OpenSharingSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (HostSharing is not null)
        {
            await HostSharing.RefreshAsync(cancellationToken).ConfigureAwait(true);
        }

        void Show()
        {
            _suppressClassroomRefresh = true;
            try
            {
                NavigateTo(new NavigationRoute(RouteKind.Classroom, Section: "sharing"));
            }
            finally
            {
                _suppressClassroomRefresh = false;
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Show();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Show);
        }
    }

    /// <summary>Opens the classroom student smart-search route.</summary>
    public void OpenStudentSmartSearch() =>
        NavigateTo(new NavigationRoute(RouteKind.Classroom, Section: "smart-search"));

    /// <summary>Opens the recommendation advisor route.</summary>
    public void OpenAdvisor() => NavigateTo(new NavigationRoute(RouteKind.Advisor));

    /// <summary>Opens the reading-plan route.</summary>
    public void OpenReadingPlan() => NavigateTo(new NavigationRoute(RouteKind.ReadingPlan));

    /// <summary>Opens the 3D bookshelf route when its capability is available.</summary>
    public void OpenBookshelf3D() => NavigateTo(new NavigationRoute(RouteKind.Shelf3D));

    /// <summary>Opens the Settings destination at a section.</summary>
    /// <param name="section">The section, or <see langword="null"/>.</param>
    public void OpenSettings(string? section = null) => NavigateTo(NavigationRoute.Settings(section));

    /// <summary>Reopens the last book in the reader.</summary>
    /// <returns>A task that completes when the reader is open.</returns>
    public Task ResumeLastBookAsync() =>
        _lastReaderBookId is { } bookId ? OpenReaderAsync(bookId) : Task.CompletedTask;

    /// <summary>Selects a library view mode and shows the catalogue.</summary>
    /// <param name="view">The view mode.</param>
    public void ShowLibraryView(CatalogueView view)
    {
        Catalogue.CurrentView = view;
        NavigateTo(NavigationRoute.Library);
    }

    // ── Command palette ───────────────────────────────────────────────────────

    /// <summary>Opens the command palette and clears the previous query.</summary>
    public void OpenCommandPalette()
    {
        IsShortcutSheetOpen = false;
        CommandPaletteQuery = string.Empty;
        OnPropertyChanged(nameof(CommandPaletteItems));
        IsCommandPaletteOpen = true;
    }

    /// <summary>Closes the command palette and clears transient query text.</summary>
    public void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        CommandPaletteQuery = string.Empty;
    }

    /// <summary>Commands matching the current palette query, best first.</summary>
    public IReadOnlyList<CommandPaletteItem> CommandPaletteItems =>
        _commands.Search(CommandPaletteQuery, command => _localization[command.LabelKey])
            .Select(command => new CommandPaletteItem(
                command.Id,
                _localization[command.LabelKey],
                command.GestureText(IsMac)))
            .ToList();

    /// <summary>Executes a registered command; the palette closes first.</summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="topLevel">The window, for commands that open pickers.</param>
    /// <param name="cancellationToken">Not used by every command.</param>
    /// <returns>A task that completes when the command finishes.</returns>
    public async Task ExecuteCommandAsync(
        string commandId,
        TopLevel? topLevel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ShellCommand command = _commands.Find(commandId)
            ?? throw new ArgumentException("The selected command is not supported.", nameof(commandId));
        cancellationToken.ThrowIfCancellationRequested();
        CloseCommandPalette();
        if (!command.IsAvailable())
        {
            return;
        }

        _commands.MarkUsed(command.Id);
        await command.Execute(topLevel).ConfigureAwait(true);
    }

    /// <summary>Runs the command bound to a key gesture, if any.</summary>
    /// <param name="key">The pressed key.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <param name="topLevel">The window.</param>
    /// <returns><see langword="true"/> when a command handled the gesture.</returns>
    public bool TryExecuteGesture(Key key, KeyModifiers modifiers, TopLevel? topLevel)
    {
        ShellCommand? command = _commands.MatchGesture(key, modifiers);
        if (command is null)
        {
            return false;
        }

        UiActions.Run(() => ExecuteCommandAsync(command.Id, topLevel), "shell.command." + command.Id);
        return true;
    }

    private void InitializeNavigation(ICapabilityState? capabilities)
    {
        _capabilities = capabilities ?? new RuntimeCapabilityState(
            classroomHostEnabled: HostSharing is not null,
            shelf3DAvailable: false);
        _capabilities.Changed += OnCapabilitiesChanged;
        _navigation = new NavigationState();
        _navigation.Changed += OnNavigationChanged;
        Catalogue.Filter.PropertyChanged += OnFilterChanged;
        Catalogue.PropertyChanged += OnCatalogueViewChanged;
        ShelfSidebar.PropertyChanged += OnShelfSidebarChanged;
        _localization.CultureChanged += (_, _) => RefreshNavigationLabels();
        if (Reader is not null)
        {
            Reader.PropertyChanged += OnReaderPropertyChanged;
        }

        RailItems.Add(new RailItemViewModel(ShellDestination.Library, "Shell.Nav.Library", "ic_lib_folder_open"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Search, "Shell.Nav.Search", "ic_search_global"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Reading, "Shell.Nav.Reading", "ic_read_open"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Advisor, "Shell.Nav.Advisor", "ic_ai_advisor"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Collections, "Shell.Nav.Collections", "ic_cat_shelf"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Activity, "Shell.Nav.Activity", "ic_index_manager"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Classroom, "Shell.Nav.Classroom", "ic_status_available"));
        RailItems.Add(new RailItemViewModel(ShellDestination.Settings, "Shell.Nav.Settings", "ic_settings"));

        _commands = BuildCommands();
        RefreshNavigationLabels();
        RefreshRailState();
    }

    private CommandRegistry BuildCommands()
    {
        static CommandGesture Primary(Key key, bool shift = false) => new(key, Primary: true, Shift: shift);
        Task Run(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        var registry = new CommandRegistry();
        registry
            .Add(new ShellCommand("nav.library", "Command.Nav.Library", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Library)), null, Primary(Key.D1)))
            .Add(new ShellCommand("nav.search", "Command.Nav.Search", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Search)), null, Primary(Key.D2), Primary(Key.F)))
            .Add(new ShellCommand("nav.reading", "Command.Nav.Reading", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Reading)), null, Primary(Key.D3)))
            .Add(new ShellCommand("nav.advisor", "Command.Nav.Advisor", CommandGroup.Navigate,
                _ => Run(OpenAdvisor), null, Primary(Key.D4)))
            .Add(new ShellCommand("nav.reading-plan", "CommandPalette.ReadingPlan", CommandGroup.Navigate,
                _ => Run(OpenReadingPlan)))
            .Add(new ShellCommand("nav.collections", "Command.Nav.Collections", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Collections)), null, Primary(Key.D5)))
            .Add(new ShellCommand("nav.activity", "Command.Nav.Activity", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Activity)), null, Primary(Key.D6)))
            .Add(new ShellCommand("nav.settings", "Command.Nav.Settings", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Settings)), null, Primary(Key.D7)))
            .Add(new ShellCommand("nav.classroom", "Command.Nav.Classroom", CommandGroup.Navigate,
                _ => Run(() => NavigateTo(ShellDestination.Classroom)), () => IsClassroomAvailable, Primary(Key.D8)))
            .Add(new ShellCommand("nav.back", "Command.Nav.Back", CommandGroup.Navigate,
                _ => Run(() => GoBack()), () => _navigation.CanGoBack, new CommandGesture(Key.Left, Alt: true)))
            .Add(new ShellCommand("nav.forward", "Command.Nav.Forward", CommandGroup.Navigate,
                _ => Run(() => GoForward()), () => _navigation.CanGoForward, new CommandGesture(Key.Right, Alt: true)))
            .Add(new ShellCommand("library.add-folder", "Command.Library.AddFolder", CommandGroup.Library,
                AddFolderFromCommandAsync))
            .Add(new ShellCommand("library.open-pdf", "Command.Library.OpenPdf", CommandGroup.Library,
                OpenPdfFromCommandAsync, null, Primary(Key.O)))
            .Add(new ShellCommand("library.rescan", "CommandPalette.Rescan", CommandGroup.Library,
                _ => RescanLibraryAsync(), null, new CommandGesture(Key.F5)))
            .Add(new ShellCommand("library.view.grid", "Command.View.Grid", CommandGroup.Library,
                _ => Run(() => ShowLibraryView(CatalogueView.Grid))))
            .Add(new ShellCommand("library.view.list", "Command.View.List", CommandGroup.Library,
                _ => Run(() => ShowLibraryView(CatalogueView.List))))
            .Add(new ShellCommand("library.view.directory", "Command.View.Directory", CommandGroup.Library,
                _ => Run(() => ShowLibraryView(CatalogueView.Directory))))
            .Add(new ShellCommand("library.view.shelf3d", "Command.View.Shelf3D", CommandGroup.Library,
                _ => Run(OpenBookshelf3D), () => IsShelf3DAvailable))
            .Add(new ShellCommand("library.filter", "Command.Library.Filter", CommandGroup.Library,
                _ => Run(() => IsFilterPanelOpen = true)))
            .Add(new ShellCommand("library.folders", "Command.Library.Folders", CommandGroup.Library,
                _ => Run(() => IsFoldersDrawerOpen = true), () => LibraryFolders is not null))
            .Add(new ShellCommand("library.reconciliation", "Command.Library.Reconciliation", CommandGroup.Library,
                _ => IsReconciliationReviewPanelOpen ? Task.CompletedTask : ToggleReconciliationReviewsAsync(),
                () => ReconciliationReviews is not null))
            .Add(new ShellCommand("library.clear-filters", "Command.Library.ClearFilters", CommandGroup.Library,
                _ => Run(Catalogue.Filter.ClearAll), () => Catalogue.Filter.HasActiveFilters))
            .Add(new ShellCommand("reader.split-view", "CommandPalette.SplitView", CommandGroup.Reader,
                _ => Run(OpenSplitView), () => SplitView is not null))
            .Add(new ShellCommand("reader.close", "Command.Reader.Close", CommandGroup.Reader,
                _ => ReturnToLibraryAsync(), () => Reader is { IsOpen: true }))
            .Add(new ShellCommand("view.toggle-theme", "CommandPalette.ToggleTheme", CommandGroup.View,
                _ => ToggleThemeAsync()))
            .Add(new ShellCommand("view.toggle-density", "CommandPalette.ToggleDensity", CommandGroup.View,
                _ => ToggleDensityAsync()))
            .Add(new ShellCommand("view.toggle-rail", "Command.View.ToggleRail", CommandGroup.View,
                _ => Run(ToggleRail)))
            .Add(new ShellCommand("app.palette", "Command.App.Palette", CommandGroup.App,
                _ => Run(OpenCommandPalette), null, Primary(Key.K), Primary(Key.P, shift: true)))
            .Add(new ShellCommand("app.shortcuts", "Command.App.Shortcuts", CommandGroup.App,
                _ => Run(() => IsShortcutSheetOpen = true), null, Primary(Key.OemQuestion)))
            .Add(new ShellCommand("app.export-diagnostics", "Command.App.ExportDiagnostics", CommandGroup.App,
                _ => ExportDiagnostics?.Invoke() ?? Task.CompletedTask, () => ExportDiagnostics is not null));
        return registry;
    }

    private Task AddFolderFromCommandAsync(TopLevel? topLevel)
    {
        if (topLevel is null)
        {
            ReportChooseFolderUnavailable();
            return Task.CompletedTask;
        }

        return ChooseFolderAsync(topLevel);
    }

    private Task OpenPdfFromCommandAsync(TopLevel? topLevel)
    {
        if (topLevel is null)
        {
            ReportOpenPdfUnavailable();
            return Task.CompletedTask;
        }

        return OpenPdfAsync(topLevel);
    }

    private void SetDrawer(LibraryDrawer drawer, bool open)
    {
        if (open)
        {
            ActiveDrawer = drawer;
        }
        else if (_activeDrawer == drawer)
        {
            ActiveDrawer = LibraryDrawer.None;
        }
    }

    private void LeaveTransientDestination()
    {
        if (!_navigation.GoBack())
        {
            NavigateTo(NavigationRoute.Library);
        }
    }

    private void OnNavigationChanged(object? sender, NavigationChangedEventArgs e)
    {
        // Drawers belong to the Library and close on any navigation (K12).
        if (_activeDrawer != LibraryDrawer.None && e.Current.Kind != RouteKind.Library)
        {
            ActiveDrawer = LibraryDrawer.None;
        }

        ReaderPlaceholderMessage = null;
        if (e.Current.Kind != RouteKind.Library)
        {
            BookDetail.IsVisible = false;
        }

        RaiseRouteChanged();

        switch (e.Current.Kind)
        {
            case RouteKind.Activity when IndexManager is not null:
                UiActions.Run(() => IndexManager.LoadAsync(), "shell.route.activity");
                break;
            case RouteKind.Classroom when HostSharing is not null && e.Previous.Kind != RouteKind.Classroom &&
                                          !_suppressClassroomRefresh:
                UiActions.Run(() => HostSharing.RefreshAsync(), "shell.route.classroom");
                break;
            case RouteKind.Reader when e.IsHistoryMove && e.Current.BookId is { } bookId &&
                                       Reader is not null && !(Reader.IsOpen && Reader.BookId == bookId):
                UiActions.Run(() => Reader.OpenAsync(bookId, e.Current.Page, CancellationToken.None), "shell.route.reader");
                break;
        }
    }

    private void OnReaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Reader.IsOpen))
        {
            OnPropertyChanged(nameof(IsReaderContentVisible));
            OnPropertyChanged(nameof(IsReadingEmptyVisible));
        }
    }

    private void OnCatalogueViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CatalogueViewModel.CurrentView) or nameof(CatalogueViewModel.IsGridView) or
            nameof(CatalogueViewModel.IsListView) or nameof(CatalogueViewModel.IsDirectoryView))
        {
            OnPropertyChanged(nameof(IsGridViewSelected));
            OnPropertyChanged(nameof(IsListViewSelected));
            OnPropertyChanged(nameof(IsDirectoryViewSelected));
        }
    }

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ActiveFilterChips));
        OnPropertyChanged(nameof(HasActiveFilterChips));
    }

    private void OnShelfSidebarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShelfSidebarViewModel.SelectedShelf))
        {
            OnPropertyChanged(nameof(CanShowSelectedCollection));
        }
    }

    private void OnCapabilitiesChanged(object? sender, EventArgs e)
    {
        void Apply()
        {
            RefreshRailState();
            OnPropertyChanged(nameof(IsShelf3DAvailable));
            OnPropertyChanged(nameof(IsStudentSmartSearchVisible));
            OnPropertyChanged(nameof(IsHostSharingVisible));
            OnPropertyChanged(nameof(IsClassroomAvailable));
            OnPropertyChanged(nameof(HasClassroomTabs));
            OnPropertyChanged(nameof(IsAdvisorSetupNeeded));
            OnPropertyChanged(nameof(CapabilityRows));
            OnPropertyChanged(nameof(IsClassroomTabsVisible));
            if (CurrentRoute.Kind == RouteKind.Classroom && !IsClassroomAvailable)
            {
                _navigation.Replace(NavigationRoute.Library);
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }

    private void RefreshNavigationLabels()
    {
        foreach (RailItemViewModel item in RailItems)
        {
            string key = item.Destination switch
            {
                ShellDestination.Search => "Navigation.Search",
                ShellDestination.Reading => "Navigation.Reading",
                ShellDestination.Advisor => "Navigation.Advisor",
                ShellDestination.Collections => "Navigation.Collections",
                ShellDestination.Activity => "Navigation.Activity",
                ShellDestination.Settings => "Navigation.Settings",
                ShellDestination.Classroom => "Navigation.Classroom",
                _ => "Navigation.Library",
            };
            item.Label = _localization[key];
            string gesture = new CommandGesture(Key.D1 + (int)item.Destination, Primary: true).Format(IsMac);
            item.ToolTip = string.Format(CultureInfo.CurrentCulture, _localization["Navigation.ShortcutHintFormat"], item.Label, gesture);
        }

        RefreshRailState();
        foreach (string name in new[]
                 {
                     nameof(DestinationHeading), nameof(AddFolderLabel), nameof(FoldersLabel), nameof(ViewSwitcherLabel),
                     nameof(FilterChipsLabel), nameof(CloseDrawerLabel), nameof(AdvisorAskLabel), nameof(AdvisorSetupHeading),
                     nameof(AdvisorSetupBody), nameof(AdvisorSetupAction), nameof(ReadingEmptyHeading), nameof(ReadingEmptyBody),
                     nameof(ReadingResumeLabel), nameof(GoToLibraryLabel), nameof(ShowCollectionLabel), nameof(CollectionsHint),
                     nameof(SettingsIntro), nameof(SettingsCapabilitiesTitle), nameof(ClassroomSmartSearchLabel),
                     nameof(ShortcutsTitle), nameof(CloseLabel), nameof(CommandPaletteCloseLabel), nameof(ShortcutRows), nameof(ExportDiagnosticsLabel),
                     nameof(CapabilityRows), nameof(ActiveFilterChips), nameof(RailToggleLabel), nameof(RailName),
                     nameof(DrawerTitle),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private void RefreshRailState()
    {
        string current = _localization["Navigation.Current"];
        foreach (RailItemViewModel item in RailItems)
        {
            item.IsSelected = item.Destination == CurrentDestination;
            item.Status = item.IsSelected ? current : string.Empty;
            item.IsVisible = item.Destination != ShellDestination.Classroom || IsClassroomAvailable;
        }
    }

    private void RaiseRailChanged()
    {
        OnPropertyChanged(nameof(IsRailCompact));
        OnPropertyChanged(nameof(IsRailExpanded));
        OnPropertyChanged(nameof(RailToggleLabel));
        OnPropertyChanged(nameof(RailToggleGlyph));
    }

    private void RaiseRouteChanged()
    {
        RefreshRailState();
        foreach (string name in new[]
                 {
                     nameof(CurrentRoute), nameof(CurrentDestination), nameof(ActiveView), nameof(DestinationHeading),
                     nameof(IsCatalogueActive), nameof(IsLibraryDestination), nameof(IsPagerVisible), nameof(IsReaderActive),
                     nameof(IsReaderContentVisible), nameof(IsReadingEmptyVisible), nameof(IsSplitViewActive),
                     nameof(IsReadingDestination), nameof(IsSharingSettingsActive), nameof(IsStudentSmartSearchActive),
                     nameof(IsClassroomActive), nameof(IsAdvisorActive), nameof(IsReadingPlanActive), nameof(IsAdvisorDestination),
                     nameof(IsBookshelf3DActive), nameof(IsSearchActive), nameof(IsSearchPanelOpen), nameof(IsCollectionsActive),
                     nameof(IsActivityActive), nameof(IsIndexManagerOpen), nameof(IsSettingsActive), nameof(HasActiveFilterChips),
                     nameof(IsGridViewSelected), nameof(IsListViewSelected), nameof(IsDirectoryViewSelected),
                     nameof(IsClassroomTabsVisible),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private CapabilityRow Row(string key, bool available) => new(
        _localization[key],
        _localization[available ? "Settings.Capability.Available" : "Settings.Capability.Unavailable"],
        _localization[key + (available ? ".On" : ".Off")],
        available);
}
