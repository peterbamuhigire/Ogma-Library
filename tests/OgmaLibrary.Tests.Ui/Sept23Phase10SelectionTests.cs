using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 10 (tasks 10.6 and 10.7, slice 4): multi-select in the grid and list, the
/// selection toolbar, and shelf membership by menu and by drop. A drop on a smart or deleted
/// shelf writes nothing and tells the user why.
/// </summary>
public sealed class Sept23Phase10SelectionTests
{
    [AvaloniaFact]
    public async Task CtrlClick_ShiftArrow_AndCtrlA_SelectTheExpectedItems()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Shell.Catalogue.CurrentView = CatalogueView.List;
        var view = new CatalogueListView { DataContext = fixture.Shell.Catalogue };
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ListBox list = view.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.Equal(SelectionMode.Multiple, list.SelectionMode & SelectionMode.Multiple);

        Click(window, list, 0, RawInputModifiers.None);
        Click(window, list, 2, RawInputModifiers.Control);
        Assert.Equal(["book-001", "book-003"], SelectedIds(fixture.Shell.Catalogue));
        Assert.Equal("book-001", fixture.Shell.Catalogue.SelectedBook?.BookId);

        Click(window, list, 3, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["book-004", "book-005"], SelectedIds(fixture.Shell.Catalogue));

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(fixture.ReadModel.Books.Count, fixture.Shell.Catalogue.SelectedBooks.Count);

        window.Close();
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task GridAndList_UseMultipleSelection()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var grid = new CatalogueGridView { DataContext = fixture.Shell.Catalogue };
        var window = new Window { Width = 1280, Height = 800, Content = grid };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ListBox bookGrid = grid.GetVisualDescendants().OfType<ListBox>()
            .Single(box => Avalonia.Automation.AutomationProperties.GetAutomationId(box) == "Catalogue.Grid");
        Assert.True(bookGrid.SelectionMode.HasFlag(SelectionMode.Multiple));
        window.Close();
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Toolbar_EnablementFollowsTheSelection()
    {
        var capabilities = new FakeCapabilities();
        Fixture fixture = await Fixture.CreateAsync(capabilities: capabilities);
        CatalogueSelectionViewModel selection = fixture.Selection;

        Assert.False(selection.IsToolbarVisible);
        Assert.False(selection.CanAddToShelf);
        Assert.False(selection.CanMarkAsRead);

        Select(fixture, "book-003");
        Assert.False(selection.IsToolbarVisible); // one book, no context action: no toolbar

        Select(fixture, "book-003", "book-004");
        Assert.True(selection.IsToolbarVisible);
        Assert.Equal(2, selection.Count);
        Assert.Contains("2", selection.CountText, StringComparison.Ordinal);
        Assert.True(selection.CanAddToShelf);
        Assert.True(selection.CanMarkAsRead);
        Assert.False(selection.CanEdit);          // bulk edit arrives in slice 5
        Assert.False(selection.IsEnrichVisible);  // no provider capability
        Assert.False(selection.CanRemoveFromShelf);

        capabilities.Enable();
        Assert.True(selection.IsEnrichVisible);
        Assert.True(selection.CanEnrich);

        // A manual shelf filter makes "Remove from shelf" a context action, even for one book.
        fixture.Shell.Catalogue.Filter.SelectedShelfId = "shelf-manual";
        Dispatcher.UIThread.RunJobs();
        Select(fixture, "book-001");
        Assert.True(selection.IsToolbarVisible);
        Assert.True(selection.CanRemoveFromShelf);

        selection.ClearSelection();
        Assert.Equal(0, selection.Count);
        Assert.False(selection.IsToolbarVisible);
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task MenuAddAndRemove_UpdateShelfIdsAndCounts()
    {
        Fixture fixture = await Fixture.CreateAsync();
        Assert.Equal(2, ManualShelf(fixture).BookCount);
        ShelfProjection target = Assert.Single(fixture.Selection.Membership.ManualShelves);
        Assert.Equal("shelf-manual", target.ShelfId);

        Select(fixture, "book-003", "book-004");
        await fixture.Selection.AddSelectionToShelfAsync(target.ShelfId);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, fixture.Writes.Added.Count);
        Assert.Contains("shelf-manual", Book(fixture, "book-003").ShelfIds);
        Assert.Contains("shelf-manual", Book(fixture, "book-004").ShelfIds);
        Assert.Equal(4, ManualShelf(fixture).BookCount);
        Assert.Equal(UserNotificationSeverity.Information, Assert.Single(fixture.Notifier.Notifications).Severity);
        Assert.Equal(["book-003", "book-004"], SelectedIds(fixture.Shell.Catalogue)); // the selection survives the refresh

        fixture.Shell.Catalogue.Filter.SelectedShelfId = "shelf-manual";
        Dispatcher.UIThread.RunJobs();
        Select(fixture, "book-003");
        Assert.True(fixture.Selection.CanRemoveFromShelf);
        await fixture.Selection.RemoveSelectionFromShelfAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("shelf-manual", Book(fixture, "book-003").ShelfIds);
        Assert.Equal(3, ManualShelf(fixture).BookCount);
        Assert.Equal(3, fixture.Shell.Catalogue.TotalFilteredCount);
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task DropOnADeletedShelf_ShowsANotice_AndWritesNothing()
    {
        Fixture fixture = await Fixture.CreateAsync();
        ShelfProjection stale = ManualShelf(fixture);
        fixture.ReadModel.Shelves.Clear(); // deleted after the row was shown

        bool added = await fixture.Selection.Membership.DropOnShelfAsync(stale, ["book-003"]);
        Dispatcher.UIThread.RunJobs();

        Assert.False(added);
        Assert.Empty(fixture.Writes.Added);
        UserNotification notice = Assert.Single(fixture.Notifier.Notifications);
        Assert.Equal("Catalogue.Shelves.Membership.Deleted", notice.MessageKey);
        Assert.Equal(UserNotificationSeverity.Warning, notice.Severity);
        Assert.Empty(fixture.Shell.ShelfSidebar.Shelves); // the stale row is gone
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task DropOnASmartShelf_IsRejected_AndWritesNothing()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.ReadModel.Shelves.Add(("shelf-smart", "Rated", true, SmartShelfQueryParser.Serialize(
            SmartShelfCriteria.ToConditions(new CatalogueFilter(MinRating: 3)))));
        await fixture.Shell.ShelfSidebar.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        ShelfProjection smart = Assert.Single(fixture.Shell.ShelfSidebar.Shelves, s => s.IsSmart);
        Assert.DoesNotContain(fixture.Selection.Membership.ManualShelves, s => s.IsSmart);

        bool added = await fixture.Selection.Membership.DropOnShelfAsync(smart, ["book-003"]);

        Assert.False(added);
        Assert.Empty(fixture.Writes.Added);
        Assert.Equal("Catalogue.Shelves.Membership.Smart", Assert.Single(fixture.Notifier.Notifications).MessageKey);
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task WriteFailure_ShowsALocalisedError_NotTheExceptionText()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Writes.FailWith = new InvalidOperationException("SQLite Error 19: FOREIGN KEY constraint failed");
        Select(fixture, "book-003", "book-004");

        await fixture.Selection.AddSelectionToShelfAsync("shelf-manual");
        Dispatcher.UIThread.RunJobs();

        UserNotification error = Assert.Single(fixture.Notifier.Notifications);
        Assert.Equal(UserNotificationSeverity.Error, error.Severity);
        Assert.Equal("Catalogue.Shelves.Membership.Failed", error.MessageKey);
        Assert.DoesNotContain("SQLite", fixture.Localization[error.MessageKey], StringComparison.Ordinal);
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task MarkAsRead_SetsFinishedForEverySelectedBook()
    {
        Fixture fixture = await Fixture.CreateAsync();
        Select(fixture, "book-002", "book-005");

        await fixture.Selection.MarkSelectionAsReadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            [("book-002", ReadingStatus.Finished), ("book-005", ReadingStatus.Finished)],
            fixture.Curation.Updates.Order());
        Assert.Equal("Catalogue.Selection.MarkedRead", Assert.Single(fixture.Notifier.Notifications).MessageKey);
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ShellView_ShowsTheToolbar_AndTheCardMenu()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var view = new CatalogueShellView { DataContext = fixture.Shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control toolbar = view.GetVisualDescendants().OfType<Control>()
            .Single(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "Catalogue.Selection.Toolbar");
        Assert.False(toolbar.IsEffectivelyVisible);

        Select(fixture, "book-001", "book-002");
        Dispatcher.UIThread.RunJobs();
        Assert.True(toolbar.IsEffectivelyVisible);
        string[] buttonIds = toolbar.GetVisualDescendants().OfType<Control>()
            .Where(c => c is Button or DropDownButton)
            .Select(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) ?? string.Empty)
            .ToArray();
        Assert.Contains("Catalogue.Selection.AddToShelf", buttonIds);
        Assert.Contains("Catalogue.Selection.Edit", buttonIds);
        Assert.Contains("Catalogue.Selection.MarkRead", buttonIds);
        Assert.Contains("Catalogue.Selection.Clear", buttonIds);
        Assert.All(
            toolbar.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible),
            button => Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(button))));

        // A click opens the Add to collection menu with one entry per manual collection (J-CAT-2).
        DropDownButton add = toolbar.GetVisualDescendants().OfType<DropDownButton>().Single();
        // Built before it opens: a MenuFlyout with no items never opens in a real window.
        Assert.NotEmpty(Assert.IsType<MenuFlyout>(add.Flyout).Items);
        Point addCentre = add.TranslatePoint(new Point(add.Bounds.Width / 2, add.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The Add button is not in the window.");
        window.MouseDown(addCentre, MouseButton.Left);
        window.MouseUp(addCentre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        var addMenu = Assert.IsType<MenuFlyout>(add.Flyout);
        Assert.True(addMenu.IsOpen, "Clicking Add to collection did not open its menu.");
        MenuItem entry = Assert.Single(addMenu.Items.OfType<MenuItem>());
        Assert.Equal("Catalogue.Selection.Shelf.shelf-manual", Avalonia.Automation.AutomationProperties.GetAutomationId(entry));
        addMenu.Hide();
        Dispatcher.UIThread.RunJobs();

        ListBoxItem card = view.GetVisualDescendants().OfType<ListBoxItem>()
            .First(item => Avalonia.Automation.AutomationProperties.GetAutomationId(item) == "Catalogue.Item.book-001");
        Assert.IsType<MenuFlyout>(card.ContextFlyout);
        window.Close();
        fixture.Shell.Dispose();
    }

    /// <summary>
    /// Regression (J-CAT-2 screenshot): below 1280 px the inspector overlays the content and hid
    /// the right end of the selection toolbar, including Clear selection.
    /// </summary>
    [AvaloniaFact]
    public async Task ShellView_OverlayInspector_DoesNotCoverTheSelectionToolbar()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var view = new CatalogueShellView { DataContext = fixture.Shell };
        var window = new Window { Width = 1200, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Select(fixture, "book-001", "book-002");
        await fixture.Shell.BookDetail.LoadBookAsync("book-001");
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(fixture.Shell.IsInspectorOverlay);
        Control inspector = view.FindControl<Control>("Inspector")!;
        Control toolbar = view.FindControl<Control>("SelectionToolbar")!;
        Assert.True(toolbar.IsEffectivelyVisible);
        Assert.True(inspector.IsEffectivelyVisible && inspector.Bounds.Width > 0, "The inspector did not open.");
        double inspectorLeft = inspector.TranslatePoint(default, window)!.Value.X;
        double toolbarRight = toolbar.TranslatePoint(new Point(toolbar.Bounds.Width, 0), window)!.Value.X;
        Assert.True(toolbarRight <= inspectorLeft, $"The toolbar ends at {toolbarRight}, under the inspector at {inspectorLeft}.");
        window.Close();
        fixture.Shell.Dispose();
    }

    /// <summary>
    /// Regression (J-CAT-1/J-CAT-2 real-window failure): with every catalogue view live, a click
    /// then a Ctrl+click must give exactly two selected books, and a background refresh that
    /// replaces the selected cards must keep both selected and keep the primary.
    /// </summary>
    [AvaloniaFact]
    public async Task ShellView_ClickCtrlClick_AndRefresh_KeepExactlyTheSelection()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var view = new CatalogueShellView { DataContext = fixture.Shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ListBox grid = view.GetVisualDescendants().OfType<ListBox>()
            .Single(box => Avalonia.Automation.AutomationProperties.GetAutomationId(box) == "Catalogue.Grid");
        Click(window, grid, 2, RawInputModifiers.None);
        Assert.Equal(["book-003"], SelectedIds(fixture.Shell.Catalogue));
        Assert.Equal("book-003", fixture.Shell.Catalogue.SelectedBook?.BookId);

        Click(window, grid, 3, RawInputModifiers.Control);
        Assert.Equal(["book-003", "book-004"], SelectedIds(fixture.Shell.Catalogue));
        Assert.Equal(2, fixture.Selection.Count);

        // A refresh that changes both cards (new shelf ids) replaces them in place.
        // The toolbar must not flicker while the refresh restores the selection: hiding it would
        // close an open Add to collection menu (J-CAT-2 real-window failure).
        var toolbarStates = new List<bool>();
        fixture.Selection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CatalogueSelectionViewModel.IsToolbarVisible))
            {
                toolbarStates.Add(fixture.Selection.IsToolbarVisible);
            }
        };
        var shelfChanges = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        fixture.Selection.Membership.ManualShelves.CollectionChanged += (_, e) => shelfChanges.Add(e.Action);
        bool canAddDropped = false;
        fixture.Selection.PropertyChanged += (_, e) =>
            canAddDropped |= e.PropertyName == nameof(CatalogueSelectionViewModel.CanAddToShelf) && !fixture.Selection.CanAddToShelf && !fixture.Selection.IsBusy;
        await fixture.Selection.AddSelectionToShelfAsync("shelf-manual");
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(false, toolbarStates);
        Assert.All(shelfChanges, action => Assert.Equal(System.Collections.Specialized.NotifyCollectionChangedAction.Replace, action));
        Assert.False(canAddDropped, "Add to collection was disabled while the shelves reloaded.");
        Assert.Equal(["book-003", "book-004"], SelectedIds(fixture.Shell.Catalogue));
        Assert.Equal("book-003", fixture.Shell.Catalogue.SelectedBook?.BookId);
        Assert.Equal(2, grid.Selection.SelectedItems.Count);
        Assert.True(grid.ContainerFromIndex(2) is ListBoxItem { IsSelected: true });
        Assert.True(
            grid.ContainerFromIndex(3) is { IsKeyboardFocusWithin: true },
            "The refreshed card lost keyboard focus, so arrow-key browsing would stop.");

        // The arrow keys still move a single selection to the next card (J-CAT-1).
        Click(window, grid, 0, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["book-002"], SelectedIds(fixture.Shell.Catalogue));
        Assert.Equal("book-002", fixture.Shell.Catalogue.SelectedBook?.BookId);

        // Choosing a book from outside the selection (home strip, directory) replaces it.
        fixture.Shell.Catalogue.SelectedBook = fixture.Shell.Catalogue.FilteredItems.Single(b => b.BookId == "book-005");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["book-005"], SelectedIds(fixture.Shell.Catalogue));
        window.Close();
        fixture.Shell.Dispose();
    }

    [AvaloniaFact]
    public void SelectionStrings_ExistInEnglishAndFrench()
    {
        var english = new InMemoryLocalizationService();
        english.SetCulture("en");
        var french = new InMemoryLocalizationService();
        french.SetCulture("fr");
        string[] keys =
        [
            "Catalogue.Selection.Toolbar", "Catalogue.Selection.CountFormat", "Catalogue.Selection.AddToShelf",
            "Catalogue.Selection.RemoveFromShelf", "Catalogue.Selection.Edit", "Catalogue.Selection.MarkRead",
            "Catalogue.Selection.Enrich", "Catalogue.Selection.Clear", "Catalogue.Selection.MarkedRead",
            "Catalogue.Selection.MarkReadFailed", "Catalogue.Selection.EnrichDone", "Catalogue.Selection.EnrichFailed",
            "Catalogue.Selection.NoShelves", "Catalogue.Shelves.Membership.Added", "Catalogue.Shelves.Membership.Removed",
            "Catalogue.Shelves.Membership.Deleted", "Catalogue.Shelves.Membership.Smart",
            "Catalogue.Shelves.Membership.Failed",
        ];
        foreach (string key in keys)
        {
            Assert.NotEqual(key, english[key]);
            Assert.NotEqual(key, french[key]);
            Assert.NotEqual(english[key], french[key]);
        }
    }

    private static void Click(Window window, ListBox list, int index, RawInputModifiers modifiers)
    {
        list.ScrollIntoView(index);
        Dispatcher.UIThread.RunJobs();
        Control container = list.ContainerFromIndex(index) ?? throw new InvalidOperationException($"No container for {index}.");
        Point centre = container.TranslatePoint(new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The container is not in the window.");
        window.MouseDown(centre, MouseButton.Left, modifiers);
        window.MouseUp(centre, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] SelectedIds(CatalogueViewModel catalogue) =>
        catalogue.SelectedBooks.Select(book => book.BookId).Order(StringComparer.Ordinal).ToArray();

    private static void Select(Fixture fixture, params string[] ids)
    {
        CatalogueViewModel catalogue = fixture.Shell.Catalogue;
        catalogue.SelectedBooks.Clear();
        foreach (string id in ids)
        {
            catalogue.SelectedBooks.Add(catalogue.FilteredItems.Single(book => book.BookId == id));
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static BookSummaryProjection Book(Fixture fixture, string id) =>
        fixture.Shell.Catalogue.FilteredItems.FirstOrDefault(book => book.BookId == id)
        ?? fixture.ReadModel.Books.Single(book => book.BookId == id);

    private static ShelfProjection ManualShelf(Fixture fixture) =>
        Assert.Single(fixture.Shell.ShelfSidebar.Shelves, shelf => shelf.ShelfId == "shelf-manual");

    private sealed record Fixture(
        MainShellViewModel Shell,
        CatalogueSelectionViewModel Selection,
        FakeReadModel ReadModel,
        FakeWriteService Writes,
        FakeCuration Curation,
        RecordingNotifier Notifier,
        InMemoryLocalizationService Localization)
    {
        public static async Task<Fixture> CreateAsync(ICapabilityState? capabilities = null)
        {
            var localization = new InMemoryLocalizationService();
            var readModel = new FakeReadModel();
            var writes = new FakeWriteService(readModel);
            var curation = new FakeCuration();
            var notifier = new RecordingNotifier();
            var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
            var sidebar = new ShelfSidebarViewModel(readModel, writes, localization, catalogue.Filter);
            var membership = new ShelfMembershipViewModel(readModel, writes, sidebar, catalogue, notifier);
            var selection = new CatalogueSelectionViewModel(
                catalogue,
                membership,
                localization,
                notifier,
                curation,
                new FakeEnrichment(),
                capabilities ?? new FakeCapabilities());
            var shell = new MainShellViewModel(
                localization,
                catalogue,
                new BookDetailViewModel(readModel, new NullNavigation(), localization),
                sidebar,
                selection: selection);
            await shell.InitializeAsync();
            Dispatcher.UIThread.RunJobs();
            return new Fixture(shell, selection, readModel, writes, curation, notifier, localization);
        }
    }

    private sealed class NullNavigation : IBookDetailNavigationService, IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingNotifier : IUserNotifier
    {
        public List<UserNotification> Notifications { get; } = [];

        public void Notify(UserNotification notification) => Notifications.Add(notification);
    }

    private sealed class FakeCapabilities : ICapabilityState
    {
        private bool _metadata;

        public event EventHandler? Changed;

        public bool IsAiConfigured => false;

        public bool IsClassroomHostEnabled => false;

        public bool IsClassroomClientConnected => false;

        public bool IsShelf3DAvailable => false;

        public bool AreMetadataProvidersEnabled => _metadata;

        public void Enable()
        {
            _metadata = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeCuration : IBookCurationService
    {
        public List<(string BookId, ReadingStatus Status)> Updates { get; } = [];

        public Task UpdateReadingStateAsync(
            string bookId,
            ReadingStatus? readingStatus = null,
            int? rating = null,
            bool? isFavourite = null,
            string reason = "user",
            CancellationToken cancellationToken = default)
        {
            if (readingStatus is { } status)
            {
                Updates.Add((bookId, status));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReadingStateHistoryEntry>> GetHistoryAsync(
            string bookId,
            int maxResults = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReadingStateHistoryEntry>>([]);
    }

    private sealed class FakeEnrichment : IBookMetadataEnrichmentService
    {
        public Task<(bool Success, string? ErrorMessage)> EnrichAsync(
            string bookId,
            string? absoluteFilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(bool, string?)>((true, null));
    }

    private sealed class FakeReadModel : ICatalogueReadModel
    {
        public List<BookSummaryProjection> Books { get; } =
        [
            Book(1, shelf: true),
            Book(2, shelf: true),
            Book(3),
            Book(4),
            Book(5),
        ];

        public List<(string Id, string Name, bool IsSmart, string? Query)> Shelves { get; } =
            [("shelf-manual", "Favourites", false, null)];

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            IEnumerable<BookSummaryProjection> source = Books;
            CatalogueFilter criteria = filter;
            if (filter.ShelfId is { } shelfId && Shelves.FirstOrDefault(s => s.Id == shelfId) is { IsSmart: true } smart)
            {
                source = SmartShelfQueryParser.TryParse(smart.Query, out IReadOnlyList<SmartShelfCondition> conditions)
                    ? SmartShelfEvaluator.Evaluate(Books, conditions)
                    : [];
                criteria = filter with { ShelfId = null };
            }

            foreach (BookSummaryProjection book in CatalogueCriteriaEvaluator.Apply(source, criteria).ToList())
            {
                yield return book;
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach ((string id, string name, bool isSmart, string? query) in Shelves.ToList())
            {
                yield return new ShelfProjection(id, name, isSmart, 0, query);
            }
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);

        private static BookSummaryProjection Book(int index, bool shelf = false) =>
            new(
                $"book-{index:D3}",
                $"Topic {index:D3}",
                [$"Writer {index}"],
                null,
                0,
                3,
                shelf ? ["shelf-manual"] : [],
                null,
                true,
                2000 + index);
    }

    private sealed class FakeWriteService(FakeReadModel readModel) : ICatalogueWriteService
    {
        public List<(string ShelfId, string BookId)> Added { get; } = [];

        public Exception? FailWith { get; set; }

        public Task<string> CreateShelfAsync(
            string name,
            bool isSmart = false,
            string? query = null,
            CancellationToken cancellationToken = default)
        {
            string id = $"shelf-{readModel.Shelves.Count + 1:D3}";
            readModel.Shelves.Add((id, name, isSmart, query));
            return Task.FromResult(id);
        }

        public Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddBookToShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default)
        {
            if (FailWith is not null)
            {
                return Task.FromException(FailWith);
            }

            Added.Add((shelfId, bookId));
            Mutate(bookId, ids => ids.Contains(shelfId) ? ids : [.. ids, shelfId]);
            return Task.CompletedTask;
        }

        public Task RemoveBookFromShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default)
        {
            Mutate(bookId, ids => ids.Where(id => id != shelfId).ToArray());
            return Task.CompletedTask;
        }

        public Task UpdateMetadataFieldAsync(
            string bookId,
            string fieldName,
            string? value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(IReadOnlyList<string> bookIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BulkEditSnapshot>>([]);

        public Task RestoreBulkEditSnapshotAsync(IReadOnlyList<BulkEditSnapshot> snapshots, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<MetadataFieldSnapshot?> CaptureMetadataFieldSnapshotAsync(string bookId, string fieldName, CancellationToken cancellationToken = default) =>
            Task.FromResult<MetadataFieldSnapshot?>(null);

        public Task RestoreMetadataFieldSnapshotAsync(MetadataFieldSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private void Mutate(string bookId, Func<IReadOnlyList<string>, IReadOnlyList<string>> change)
        {
            int index = readModel.Books.FindIndex(book => book.BookId == bookId);
            readModel.Books[index] = readModel.Books[index] with { ShelfIds = change(readModel.Books[index].ShelfIds) };
        }
    }
}
