using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Navigation;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.App.Views.Shell;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 07 (K11, K12, K17, UX-003, UX-005): the routed shell. Route exclusivity and
/// history, drawers, filter chips, capability-bound entries, toolbar overflow at four widths,
/// responsive reachability at four window sizes, focus order, shortcuts and the command
/// registry.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    // ── Route model (T07.2) ─────────────────────────────────────────────────────

    [Fact]
    public void Navigation_State_BackAndForwardRestoreRouteAndScrollOffset()
    {
        var state = new NavigationState();
        double scroll = 0;
        state.ScrollOffsetProvider = () => scroll;
        var changes = new List<NavigationChangedEventArgs>();
        state.Changed += (_, e) => changes.Add(e);

        scroll = 480;
        Assert.True(state.Navigate(new NavigationRoute(RouteKind.Search)));
        scroll = 0;
        Assert.True(state.Navigate(NavigationRoute.Settings("ai")));
        Assert.False(state.Navigate(NavigationRoute.Settings("ai")));

        Assert.True(state.GoBack());
        Assert.Equal(RouteKind.Search, state.Current.Kind);
        Assert.True(state.GoBack());
        Assert.Equal(NavigationRoute.Library, state.Current);
        Assert.Equal(480, changes[^1].RestoredScrollOffset);
        Assert.True(changes[^1].IsHistoryMove);
        Assert.False(state.CanGoBack);

        Assert.True(state.GoForward());
        Assert.True(state.GoForward());
        Assert.Equal(NavigationRoute.Settings("ai"), state.Current);
        Assert.False(state.CanGoForward);

        // A new route truncates the forward history.
        state.GoBack();
        state.Navigate(new NavigationRoute(RouteKind.Activity));
        Assert.False(state.CanGoForward);
    }

    [Fact]
    public void Navigation_State_HistoryIsBounded()
    {
        var state = new NavigationState();
        for (int i = 0; i < NavigationState.MaxHistory * 2; i++)
        {
            state.Navigate(new NavigationRoute(i % 2 == 0 ? RouteKind.Search : RouteKind.Activity, Section: i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        int backs = 0;
        while (state.GoBack())
        {
            backs++;
        }

        Assert.Equal(NavigationState.MaxHistory - 1, backs);
    }

    [AvaloniaFact]
    public void Navigation_OpeningADestinationClosesThePreviousOneAndItsDrawers()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new EmptyCatalogueReadModel());

        shell.IsFilterPanelOpen = true;
        Assert.True(shell.IsDrawerOpen);

        shell.IsSearchPanelOpen = true;
        Assert.True(shell.IsSearchActive);
        Assert.False(shell.IsCatalogueActive);
        Assert.False(shell.IsDrawerOpen);

        shell.IsIndexManagerOpen = true;
        Assert.True(shell.IsActivityActive);
        Assert.False(shell.IsSearchActive);

        shell.OpenAdvisor();
        shell.OpenReadingPlan();
        Assert.True(shell.IsReadingPlanActive);
        Assert.False(shell.IsAdvisorActive);
        Assert.Equal(ShellDestination.Advisor, shell.CurrentDestination);

        // Exactly one route-derived surface is active at any time (K12).
        bool[] surfaces =
        [
            shell.IsCatalogueActive, shell.IsSearchActive, shell.IsReaderActive, shell.IsSplitViewActive,
            shell.IsAdvisorActive, shell.IsReadingPlanActive, shell.IsCollectionsActive, shell.IsActivityActive,
            shell.IsSettingsActive, shell.IsClassroomActive, shell.IsBookshelf3DActive,
        ];
        Assert.Single(surfaces, active => active);

        Assert.True(shell.GoBack());
        Assert.True(shell.IsAdvisorActive);
        shell.Dispose();
    }

    [AvaloniaFact]
    public void Navigation_DrawersAreMutuallyExclusiveAndEscapeCloses()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new EmptyCatalogueReadModel());
        shell.IsSearchPanelOpen = true;

        shell.IsFoldersDrawerOpen = true;
        Assert.True(shell.IsCatalogueActive);
        Assert.Equal(LibraryDrawer.Folders, shell.ActiveDrawer);

        shell.IsFilterPanelOpen = true;
        Assert.Equal(LibraryDrawer.Filter, shell.ActiveDrawer);
        Assert.False(shell.IsFoldersDrawerOpen);

        (Window window, CatalogueShellView view) = ShowShell(shell, 1280, 800);
        view.Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(LibraryDrawer.None, shell.ActiveDrawer);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Navigation_HiddenFilterIsAlwaysVisibleAsAChip()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = CreateLayoutShell(localization, new SeededLayoutReadModel(5));
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        shell.IsFilterPanelOpen = true;
        shell.Catalogue.Filter.TitleSearch = "Book 01";
        shell.IsSearchPanelOpen = true;
        shell.OpenCatalogue();

        // The drawer closed on navigation, but the filter still applies, so it must be shown.
        Assert.False(shell.IsFilterPanelOpen);
        Assert.True(shell.HasActiveFilterChips);
        FilterChip chip = Assert.Single(shell.ActiveFilterChips);
        Assert.Equal("Title: Book 01", chip.Label);
        Assert.Equal("Remove filter: Title: Book 01", chip.RemoveName);

        (Window window, CatalogueShellView view) = ShowShell(shell, 1280, 800);
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == chip.Label && block.IsEffectivelyVisible);

        shell.RemoveFilter(chip.Id);
        Assert.False(shell.HasActiveFilterChips);
        window.Close();
        shell.Dispose();
    }

    // ── Capabilities (T07.6, K17) ────────────────────────────────────────────────

    [AvaloniaFact]
    public void Navigation_Standalone_HidesClassroomAnd3DAndExplainsTheAdvisor()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new EmptyCatalogueReadModel());

        Assert.False(shell.IsClassroomAvailable);
        Assert.False(shell.RailItems.Single(item => item.Destination == ShellDestination.Classroom).IsVisible);
        Assert.False(shell.IsStudentSmartSearchVisible);
        Assert.False(shell.IsShelf3DAvailable);
        Assert.DoesNotContain(shell.CommandPaletteItems, item => item.Id is "nav.classroom" or "library.view.shelf3d");
        Assert.True(shell.IsAdvisorSetupNeeded);

        // A stale Classroom route never shows a blank page: it explains in Settings instead.
        shell.NavigateTo(new NavigationRoute(RouteKind.Classroom));
        Assert.True(shell.IsSettingsActive);
        Assert.Equal("classroom", shell.CurrentRoute.Section);
        Assert.Contains(shell.CapabilityRows, row => !row.IsAvailable && row.Detail.Length > 0);
        shell.Dispose();
    }

    [AvaloniaFact]
    public void Navigation_Capabilities_ShowClassroomAndAdvisorWhenAvailable()
    {
        var localization = new InMemoryLocalizationService();
        var capabilities = new RuntimeCapabilityState(classroomHostEnabled: false, isAiConfigured: () => true);
        var filter = new CatalogueFilterViewModel();
        var readModel = new EmptyCatalogueReadModel();
        var shell = new MainShellViewModel(
            localization,
            new CatalogueViewModel(readModel, new NullNavigation(), localization),
            new BookDetailViewModel(readModel, new NullNavigation(), localization),
            new ShelfSidebarViewModel(readModel, new NoOpCatalogueWriteService(), localization, filter),
            capabilities: capabilities);

        Assert.False(shell.IsAdvisorSetupNeeded);
        Assert.False(shell.IsClassroomAvailable);
        shell.Dispose();
    }

    // ── Toolbar overflow (T07.4) ────────────────────────────────────────────────

    [Fact]
    public void Toolbar_OverflowAlgorithm_KeepsPinnedAndDropsLowestPriorityFirst()
    {
        ToolbarSlot[] slots =
        [
            new(0, 100), // pinned
            new(1, 100),
            new(2, 50),
            new(2, 50), // same priority: overflows as a group
            new(3, 100),
        ];

        Assert.Equal([true, true, true, true, true], ToolbarOverflow.Fit(slots, 400));
        Assert.Equal([true, true, true, true, false], ToolbarOverflow.Fit(slots, 399));
        Assert.Equal([true, true, false, false, false], ToolbarOverflow.Fit(slots, 250));
        Assert.Equal([true, false, false, false, false], ToolbarOverflow.Fit(slots, 150));
        Assert.Equal([true, false, false, false, false], ToolbarOverflow.Fit(slots, 10));
    }

    [AvaloniaTheory]
    [InlineData(860, new[] { "AddFolderButton", "FilterButton", "ViewSwitcher", "RescanButton" })]
    [InlineData(1100, new[] { "AddFolderButton", "FilterButton", "ViewSwitcher", "RescanButton" })]
    [InlineData(1280, new[] { "AddFolderButton", "FilterButton", "ViewSwitcher", "RescanButton", "SortGroup", "FoldersButton" })]
    [InlineData(1920, new[] { "AddFolderButton", "FilterButton", "ViewSwitcher", "RescanButton", "SortGroup", "FoldersButton", "CountText" })]
    public async Task Toolbar_LibraryItemsAreVisibleOrInOverflowAtEachWidth(int width, string[] expectedVisible)
    {
        MainShellViewModel shell = CreateFoldersShell(
            new InMemoryLocalizationService(),
            new FakeRoots("Science"),
            new FakeMonitor(),
            new FakeAttention(0));
        await shell.LibraryFolders!.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        (Window window, CatalogueShellView view) = ShowShell(shell, width, 800);

        PriorityToolbarPanel toolbar = view.FindControl<PriorityToolbarPanel>("LibraryToolbar")!;
        string[] visible = [.. toolbar.Children
            .Where(child => child.IsVisible && !PriorityToolbarPanel.GetIsOverflowed(child))
            .Select(child => child.Name!)];
        string[] overflowed = [.. toolbar.OverflowedChildren.Select(child => child.Name!)];

        Assert.Equal(expectedVisible, visible);
        Assert.Contains("AddFolderButton", visible);
        Assert.True(visible.Length <= 7, "A destination toolbar shows at most 7 controls.");

        // Lower-priority items always overflow before higher-priority ones.
        if (overflowed.Length > 0)
        {
            int highestVisible = toolbar.Children.Where(child => visible.Contains(child.Name)).Max(PriorityToolbarPanel.GetPriority);
            int lowestOverflowed = toolbar.OverflowedChildren.Min(PriorityToolbarPanel.GetPriority);
            Assert.True(highestVisible < lowestOverflowed);
        }

        // Every visible toolbar item is fully inside the window and hit-testable (K11).
        foreach (Control child in toolbar.Children.Where(child => child.IsVisible && !PriorityToolbarPanel.GetIsOverflowed(child)))
        {
            AssertInsideWindow(window, child, child.Name!);
        }

        // Every overflowed item is offered by the More menu.
        Button more = view.FindControl<DropDownButton>("LibraryMoreButton")!;
        AssertInsideWindow(window, more, "More");
        var flyout = (MenuFlyout)((DropDownButton)more).Flyout!;
        flyout.ShowAt(more);
        Dispatcher.UIThread.RunJobs();
        string[] menu = [.. flyout.Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? string.Empty)];
        Assert.Contains(shell.OpenPdfText, menu);
        if (overflowed.Contains("SortGroup"))
        {
            Assert.Contains(shell.SortLabel, menu);
        }

        if (overflowed.Contains("FoldersButton"))
        {
            Assert.Contains(shell.FoldersLabel, menu);
        }

        flyout.Hide();
        window.Close();
        shell.Dispose();
    }

    // ── Responsive reachability (objective 4) ─────────────────────────────────────

    [AvaloniaTheory]
    [InlineData(860, 560)]
    [InlineData(1280, 800)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    public async Task Navigation_EveryDestinationAndPrimaryActionIsReachable(int width, int height)
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new SeededLayoutReadModel(17));
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();
        (Window window, CatalogueShellView view) = ShowShell(shell, width, height);

        Assert.Equal(width < MainShellViewModel.RailCompactBelow, shell.IsRailCompact);
        Assert.Equal(width < MainShellViewModel.InspectorOverlayBelow, shell.IsInspectorOverlay);

        List<Button> railButtons = [.. view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.DataContext is RailItemViewModel && button.IsEffectivelyVisible)];
        Assert.Equal(shell.RailItems.Count(item => item.IsVisible), railButtons.Count);
        foreach (Button button in railButtons)
        {
            var item = (RailItemViewModel)button.DataContext!;
            AssertInsideWindow(window, button, item.Label);

            // Every visible rail item leads to a painted, non-empty destination.
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, button));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(item.Destination, shell.CurrentDestination);
            TextBlock heading = view.GetVisualDescendants().OfType<TextBlock>()
                .Single(block => Avalonia.Automation.AutomationProperties.GetAutomationId(block) == "Shell.Heading");
            Assert.Equal(item.Label, heading.Text);
            AssertInsideWindow(window, heading, "heading of " + item.Label);
        }

        shell.OpenCatalogue();
        Dispatcher.UIThread.RunJobs();
        Button chooseFolder = view.GetVisualDescendants().OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "Shell.Action.ChooseFolder");
        AssertInsideWindow(window, chooseFolder, "Add folder");

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public void Navigation_FocusOrderIsRailThenToolbarThenContent()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new EmptyCatalogueReadModel());
        (Window window, CatalogueShellView view) = ShowShell(shell, 1280, 800);

        Button firstRail = view.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is RailItemViewModel);
        firstRail.Focus(NavigationMethod.Tab);
        var order = new List<string>();
        IInputElement? current = firstRail;
        for (int i = 0; i < 20 && current is not null; i++)
        {
            string? id = current is Control control ? Avalonia.Automation.AutomationProperties.GetAutomationId(control) : null;
            order.Add(id ?? current.GetType().Name);
            current = KeyboardNavigationHandler.GetNext(current, NavigationDirection.Next);
        }

        int repeat = order.IndexOf(order[0], 1);
        if (repeat > 0)
        {
            order = order[..repeat];
        }

        int lastRail = order.FindLastIndex(id => id.StartsWith("Shell.Nav.", StringComparison.Ordinal));
        int firstToolbar = order.FindIndex(id => id == "Shell.Action.ChooseFolder");
        int firstContent = order.FindIndex(id => id.StartsWith("Catalogue.EmptyState", StringComparison.Ordinal));
        Assert.True(lastRail >= 0 && firstToolbar > lastRail, string.Join(" > ", order));
        Assert.True(firstContent > firstToolbar, string.Join(" > ", order));

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public void Navigation_KeyboardShortcutsReachEveryDestination()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new EmptyCatalogueReadModel());
        (Window window, CatalogueShellView view) = ShowShell(shell, 1280, 800);

        (PhysicalKey Key, ShellDestination Destination)[] map =
        [
            (PhysicalKey.Digit2, ShellDestination.Search),
            (PhysicalKey.Digit3, ShellDestination.Reading),
            (PhysicalKey.Digit4, ShellDestination.Advisor),
            (PhysicalKey.Digit5, ShellDestination.Collections),
            (PhysicalKey.Digit6, ShellDestination.Activity),
            (PhysicalKey.Digit7, ShellDestination.Settings),
            (PhysicalKey.Digit1, ShellDestination.Library),
        ];
        foreach ((PhysicalKey key, ShellDestination destination) in map)
        {
            view.Focus();
            window.KeyPressQwerty(key, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(destination, shell.CurrentDestination);
        }

        view.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ShellDestination.Settings, shell.CurrentDestination);

        view.Focus();
        window.KeyPressQwerty(PhysicalKey.Slash, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsShortcutSheetOpen);
        Assert.Contains(shell.ShortcutRows, row => row.Gesture == "Ctrl+1" || row.Gesture.StartsWith('⌘'));

        window.Close();
        shell.Dispose();
    }

    // ── Command registry (T07.7, UX-003) ────────────────────────────────────────

    [AvaloniaFact]
    public void CommandRegistry_EveryAvailableCommandIsInThePaletteWithEnglishAndFrenchLabels()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = CreateLayoutShell(localization, new EmptyCatalogueReadModel());
        shell.ExportDiagnostics = () => Task.CompletedTask;

        string[] palette = [.. shell.CommandPaletteItems.Select(item => item.Id)];
        foreach (ShellCommand command in shell.Commands.All.Where(command => command.IsAvailable()))
        {
            Assert.Contains(command.Id, palette);
        }

        Assert.True(shell.Commands.All.Count >= 25, $"Only {shell.Commands.All.Count} commands are registered.");
        foreach (ShellDestination destination in Enum.GetValues<ShellDestination>())
        {
            Assert.Contains(shell.Commands.All, command => command.Id == "nav." + destination.ToString().ToLowerInvariant());
        }

        foreach (string culture in new[] { "en", "fr" })
        {
            localization.SetCulture(culture);
            foreach (ShellCommand command in shell.Commands.All)
            {
                string label = localization[command.LabelKey];
                Assert.False(label.StartsWith('⟦'), $"{command.Id} has no {culture} label ({command.LabelKey}).");
            }

            foreach (RailItemViewModel item in shell.RailItems)
            {
                Assert.False(item.Label.StartsWith('⟦'), $"Rail item {item.Destination} has no {culture} label.");
            }
        }

        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task CommandRegistry_FuzzyMatchingRecentsAndExecutionClosePalette()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new EmptyCatalogueReadModel());

        shell.OpenCommandPalette();
        shell.CommandPaletteQuery = "colections";
        Assert.Equal("nav.collections", shell.CommandPaletteItems[0].Id);
        shell.CommandPaletteQuery = "go set";
        Assert.Equal("nav.settings", shell.CommandPaletteItems[0].Id);
        Assert.Equal("Ctrl+7", shell.CommandPaletteItems[0].Hint.Replace("⌘", "Ctrl+", StringComparison.Ordinal));

        await shell.ExecuteCommandAsync("nav.activity");
        Assert.False(shell.IsCommandPaletteOpen);
        Assert.True(shell.IsActivityActive);

        shell.OpenCommandPalette();
        Assert.Equal("nav.activity", shell.CommandPaletteItems[0].Id);
        await Assert.ThrowsAsync<ArgumentException>(() => shell.ExecuteCommandAsync("no-such-command"));
        shell.Dispose();
    }

    private static string Describe(IInputElement? element)
    {
        var parts = new List<string>();
        for (Visual? v = element as Visual; v is not null && parts.Count < 8; v = v.GetVisualParent())
        {
            parts.Add($"{v.GetType().Name}#{(v as Control)?.Name}");
        }

        return parts.Count == 0 ? "nothing" : string.Join(" < ", parts);
    }

    private static void AssertInsideWindow(Window window, Control control, string what)
    {
        Point? origin = control.TranslatePoint(default, window);
        Assert.True(origin.HasValue, $"{what} is not in the window.");
        var bounds = new Rect(origin.Value, control.Bounds.Size);
        var client = new Rect(window.ClientSize);
        Assert.True(bounds.Width > 0 && bounds.Height > 0, $"{what} has no size: {bounds}.");
        Assert.True(client.Contains(bounds), $"{what} at {bounds} is outside the window {client} (K11).");
        // Hit testing reads the rendered scene, so render the latest layout first.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        IInputElement? hit = window.InputHitTest(bounds.Center);
        Assert.True(
            hit is Visual visual && (visual == control || control.IsVisualAncestorOf(visual)),
            $"{what} centre {bounds.Center} is covered by {Describe(hit)}.");
    }
}
