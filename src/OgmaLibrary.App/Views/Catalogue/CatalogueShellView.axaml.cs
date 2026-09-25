using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Shell;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// Code-behind for the routed shell (FR-CAT-001; Sept-23 Phase 07). It forwards input to the
/// view model, keeps the responsive state in step with the width, builds the "More" menu from
/// the overflowed toolbar items and restores the catalogue scroll position on Back.
/// </summary>
public partial class CatalogueShellView : UserControl
{
    private MainShellViewModel? _viewModel;

    /// <summary>Initializes a new instance of <see cref="CatalogueShellView"/>.</summary>
    public CatalogueShellView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LibraryToolbar.OverflowChanged += (_, _) =>
            Dispatcher.UIThread.Post(RebuildMoreMenu, DispatcherPriority.Background);
        SizeChanged += OnSizeChanged;
        AddHandler(PointerPressedEvent, OnShellPointerPressed, RoutingStrategies.Tunnel);
    }

    private MainShellViewModel? ViewModel => DataContext as MainShellViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Navigation.Changed -= OnNavigationChanged;
            _viewModel.Navigation.ScrollOffsetProvider = null;
        }

        _viewModel = DataContext as MainShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Navigation.Changed += OnNavigationChanged;
            _viewModel.Navigation.ScrollOffsetProvider = () => CatalogueScrollViewer()?.Offset.Y ?? 0;
            if (Bounds.Width > 0)
            {
                _viewModel.UpdateShellWidth(Bounds.Width);
            }

            ApplyInspectorLayout();
            RebuildMoreMenu();
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ViewModel?.UpdateShellWidth(e.NewSize.Width);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainShellViewModel.IsInspectorOverlay))
        {
            ApplyInspectorLayout();
        }
        else if (e.PropertyName is nameof(MainShellViewModel.DestinationHeading) or
                 nameof(MainShellViewModel.ExportDiagnosticsLabel))
        {
            // Labels follow the culture.
            RebuildMoreMenu();
        }
    }

    /// <summary>Below 1280 px the inspector overlays the content instead of taking a column.</summary>
    private void ApplyInspectorLayout()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        Grid.SetColumn(Inspector, vm.IsInspectorOverlay ? 1 : 2);
        Inspector.HorizontalAlignment = vm.IsInspectorOverlay
            ? Avalonia.Layout.HorizontalAlignment.Right
            : Avalonia.Layout.HorizontalAlignment.Stretch;
        Inspector.ZIndex = vm.IsInspectorOverlay ? 1 : 0;
    }

    private void OnNavigationChanged(object? sender, NavigationChangedEventArgs e)
    {
        if (e.Current.Kind == RouteKind.Search)
        {
            FocusSearchPanel();
        }

        if (e.IsHistoryMove && e.Current.Kind == RouteKind.Library && e.RestoredScrollOffset > 0)
        {
            double offset = e.RestoredScrollOffset;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (CatalogueScrollViewer() is { } scroller)
                    {
                        scroller.Offset = scroller.Offset.WithY(offset);
                    }
                },
                DispatcherPriority.Background);
        }
    }

    private ScrollViewer? CatalogueScrollViewer() =>
        this.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault(scroller => scroller.IsEffectivelyVisible &&
                                        (scroller.FindAncestorOfType<CatalogueGridView>() is not null ||
                                         scroller.FindAncestorOfType<CatalogueListView>() is not null));

    private void OnShellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsXButton1Pressed)
        {
            vm.GoBack();
            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            vm.GoForward();
            e.Handled = true;
        }
    }

    // ── Keyboard map (UX-005) ──────────────────────────────────────────────────

    private void CatalogueShellView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm || e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (vm.CloseDrawer())
            {
                e.Handled = true;
            }
            else if (vm.IsSearchActive)
            {
                vm.IsSearchPanelOpen = false;
                e.Handled = true;
            }

            return;
        }

        if (vm.TryExecuteGesture(e.Key, e.KeyModifiers, TopLevel.GetTopLevel(this)))
        {
            e.Handled = true;
        }
    }

    private void Rail_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down or Key.Home or Key.End) || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        List<Button> items = RailList.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .ToList();
        if (items.Count == 0)
        {
            return;
        }

        int current = items.FindIndex(button => button.IsFocused);
        int next = e.Key switch
        {
            Key.Home => 0,
            Key.End => items.Count - 1,
            Key.Up => current <= 0 ? items.Count - 1 : current - 1,
            _ => current < 0 || current >= items.Count - 1 ? 0 : current + 1,
        };
        items[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void FocusSearchPanel() =>
        Dispatcher.UIThread.Post(() => SearchPanel.FocusSearchBox(), DispatcherPriority.Input);

    // ── Rail ──────────────────────────────────────────────────────────────────

    private void RailItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RailItemViewModel item })
        {
            ViewModel?.NavigateTo(item.Destination);
        }
    }

    private void ToggleRail_Click(object? sender, RoutedEventArgs e) => ViewModel?.ToggleRail();

    // ── Library toolbar and More menu ───────────────────────────────────────────

    /// <summary>
    /// Rebuilds the More menu from the overflowed toolbar items. It runs when the overflow set
    /// changes, never while the flyout is opening (an empty flyout would not open).
    /// </summary>
    private void RebuildMoreMenu()
    {
        if (LibraryMoreButton.Flyout is not MenuFlyout flyout || ViewModel is not { } vm)
        {
            return;
        }

        var items = new List<MenuItem>();
        foreach (Control child in LibraryToolbar.OverflowedChildren)
        {
            if (child == SortGroup)
            {
                items.Add(SortMenu(vm));
                continue;
            }

            foreach (Button button in ButtonsOf(child))
            {
                items.Add(MenuFor(button));
            }
        }

        items.Add(Command(vm.OpenPdfText, "Shell.More.OpenPdf", () => OpenPdfButton_Click(LibraryMoreButton, new RoutedEventArgs())));
        if (vm.ReconciliationReviews is not null)
        {
            items.Add(Command(vm.ReconciliationReviewLabel, "Shell.More.Reconciliation", () =>
                UiActions.Run(() => vm.ToggleReconciliationReviewsAsync(), "catalogue.reconciliation_review_toggle_click")));
        }

        if (vm.ExportDiagnostics is not null)
        {
            items.Add(Command(vm.ExportDiagnosticsLabel, "Shell.More.ExportDiagnostics", () =>
                UiActions.Run(() => vm.ExecuteCommandAsync("app.export-diagnostics", TopLevel.GetTopLevel(this)), "shell.more.export_diagnostics")));
        }

        flyout.Items.Clear();
        foreach (MenuItem item in items)
        {
            flyout.Items.Add(item);
        }
    }

    private static IEnumerable<Button> ButtonsOf(Control child) =>
        child is Button button
            ? [button]
            : child.GetLogicalDescendants().OfType<Button>().Where(candidate => candidate.IsVisible);

    private static MenuItem MenuFor(Button button)
    {
        string name = AutomationPropertiesName(button);
        var item = new MenuItem { Header = name };
        Avalonia.Automation.AutomationProperties.SetName(item, name);
        if (Avalonia.Automation.AutomationProperties.GetAutomationId(button) is { Length: > 0 } id)
        {
            Avalonia.Automation.AutomationProperties.SetAutomationId(item, "Shell.More." + id[(id.LastIndexOf('.') + 1)..]);
        }

        item.Click += (_, _) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        return item;
    }

    private static MenuItem Command(string label, string automationId, Action action)
    {
        var item = new MenuItem { Header = label };
        Avalonia.Automation.AutomationProperties.SetName(item, label);
        Avalonia.Automation.AutomationProperties.SetAutomationId(item, automationId);
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem SortMenu(MainShellViewModel vm)
    {
        var sort = new MenuItem { Header = vm.SortLabel };
        Avalonia.Automation.AutomationProperties.SetName(sort, vm.SortLabel);
        Avalonia.Automation.AutomationProperties.SetAutomationId(sort, "Shell.More.Sort");
        var children = new List<MenuItem>();
        foreach (CatalogueSortField field in vm.Catalogue.Filter.SortOptions)
        {
            CatalogueSortField captured = field;
            children.Add(Command(field.ToString(), "Shell.More.Sort." + field, () => vm.Catalogue.Filter.SortField = captured));
        }

        children.Add(Command(vm.Catalogue.Filter.SortDirectionText, "Shell.More.SortDirection", vm.Catalogue.Filter.ToggleSortDirection));
        sort.ItemsSource = children;
        return sort;
    }

    private static string AutomationPropertiesName(Control control) =>
        Avalonia.Automation.AutomationProperties.GetName(control) ?? string.Empty;

    private void FilterToggle_Click(object? sender, RoutedEventArgs e) => ViewModel?.ToggleFilterPanel();

    private void FoldersToggle_Click(object? sender, RoutedEventArgs e) => ViewModel?.ToggleFoldersDrawer();

    private void NeedsAttention_Click(object? sender, RoutedEventArgs e) => ViewModel?.ShowNeedsAttention();

    private void CloseDrawer_Click(object? sender, RoutedEventArgs e) => ViewModel?.CloseDrawer();

    private void Rescan_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ViewModel?.RescanLibraryAsync() ?? Task.CompletedTask, "catalogue.rescan_click");

    private void RemoveFilterChip_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FilterChip chip })
        {
            ViewModel?.RemoveFilter(chip.Id);
        }
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e) => ViewModel?.Catalogue.Filter.ClearAll();

    private void ToggleSortDirection_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.Catalogue.Filter.ToggleSortDirection();

    private void GridViewButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.ShowLibraryView(CatalogueView.Grid);

    private void ListViewButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.ShowLibraryView(CatalogueView.List);

    private void DirectoryViewButton_Click(object? sender, RoutedEventArgs e) =>
        ViewModel?.ShowLibraryView(CatalogueView.Directory);

    private void Bookshelf3DButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenBookshelf3D();

    private void ReconciliationReviewPanel_CloseRequested(object? sender, EventArgs e) =>
        ViewModel?.CloseReconciliationReviews();

    private void PreviousPage_Click(object? sender, RoutedEventArgs e) => ViewModel?.Catalogue.GoToPreviousPage();

    private void NextPage_Click(object? sender, RoutedEventArgs e) => ViewModel?.Catalogue.GoToNextPage();

    // ── Destination actions ─────────────────────────────────────────────────────

    private void LibraryButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenCatalogue();

    private void BackToLibrary_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ViewModel?.ReturnToLibraryAsync() ?? Task.CompletedTask, "catalogue.library_button_click");

    private void ResumeLastBook_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ViewModel?.ResumeLastBookAsync() ?? Task.CompletedTask, "reading.resume_click");

    private void SplitViewButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenSplitView();

    private void AdvisorButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenAdvisor();

    private void ReadingPlanButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenReadingPlan();

    private void AdvisorSettingsRoute_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenSettings("ai");

    private void ShowCollection_Click(object? sender, RoutedEventArgs e) => ViewModel?.ShowSelectedCollectionInLibrary();

    private void StudentSmartSearchButton_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenStudentSmartSearch();

    private void SharingSettingsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ViewModel?.OpenSharingSettingsAsync() ?? Task.CompletedTask, "catalogue.sharing_settings_button_click");

    private void AddLooseFolder_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ViewModel?.AddLooseFolderAsync() ?? Task.CompletedTask, "catalogue.add_loose_folder_click");

    private void ChooseFolderButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ChooseFolderButton_ClickAsync(sender), "catalogue.choose_folder_button_click");

    private async Task ChooseFolderButton_ClickAsync(object? sender)
    {
        if (ViewModel is { } vm)
        {
            if (ResolveTopLevel(sender) is { } topLevel)
            {
                await vm.ChooseFolderAsync(topLevel).ConfigureAwait(true);
            }
            else
            {
                vm.ReportChooseFolderUnavailable();
            }
        }
    }

    private void OpenPdfButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => OpenPdfButton_ClickAsync(sender), "catalogue.open_pdf_button_click");

    private async Task OpenPdfButton_ClickAsync(object? sender)
    {
        if (ViewModel is { } vm)
        {
            if (ResolveTopLevel(sender) is { } topLevel)
            {
                await vm.OpenPdfAsync(topLevel).ConfigureAwait(true);
            }
            else
            {
                vm.ReportOpenPdfUnavailable();
            }
        }
    }

    private TopLevel? ResolveTopLevel(object? sender)
    {
        if (sender is Control source && TopLevel.GetTopLevel(source) is { } senderTopLevel)
        {
            return senderTopLevel;
        }

        if (TopLevel.GetTopLevel(this) is { } viewTopLevel)
        {
            return viewTopLevel;
        }

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
