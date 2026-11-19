using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11, 11.9 and 11.10: the inspector in the shell. Esc closes drawer, then
/// inspector, then search; closing returns focus to the selected card without changing the
/// selection; the width follows the shell, resizes by pointer and keyboard within 320–560;
/// nothing selected means no panel and no column width.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaFact]
    public async Task Inspector_Escape_ClosesDrawerThenInspectorThenSearch()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new SeededLayoutReadModel(3));
        await shell.InitializeAsync();
        (Window window, CatalogueShellView view) = ShowShell(shell, 1440, 900);

        shell.IsFilterPanelOpen = true;
        shell.BookDetail.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        view.Focus();

        PressEscape(window);
        Assert.False(shell.IsFilterPanelOpen);
        Assert.True(shell.BookDetail.IsVisible);

        PressEscape(window);
        Assert.False(shell.BookDetail.IsVisible);

        shell.IsSearchPanelOpen = true;
        shell.BookDetail.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        view.Focus();

        PressEscape(window);
        Assert.False(shell.BookDetail.IsVisible);
        Assert.True(shell.IsSearchActive);

        PressEscape(window);
        Assert.False(shell.IsSearchActive);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Inspector_Escape_KeepsSelectionAndReturnsFocusToTheSelectedCard()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new SeededLayoutReadModel(4));
        await shell.InitializeAsync();
        (Window window, CatalogueShellView view) = ShowShell(shell, 1440, 900);
        shell.Catalogue.SelectedBook = shell.Catalogue.FilteredItems[2];
        shell.BookDetail.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        Button close = view.GetVisualDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetAutomationId(b) == "Detail.Action.Close");
        close.Focus();
        Dispatcher.UIThread.RunJobs();

        PressEscape(window);
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.BookDetail.IsVisible);
        Assert.Equal("layout-02", shell.Catalogue.SelectedBook?.BookId);
        ListBox grid = view.GetVisualDescendants().OfType<ListBox>()
            .Single(l => AutomationProperties.GetAutomationId(l) == "Catalogue.Grid");
        Assert.True(grid.ContainerFromIndex(2) is { IsKeyboardFocusWithin: true }, "Focus did not return to the selected card.");

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Inspector_NothingSelected_IsHiddenAndTakesNoColumnWidth()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new SeededLayoutReadModel(3));
        await shell.InitializeAsync();
        (Window window, CatalogueShellView view) = ShowShell(shell, 1440, 900);

        Assert.False(view.FindControl<BookDetailView>("Inspector")!.IsVisible);
        Grid shellGrid = view.FindControl<Grid>("ShellGrid")!;
        Assert.Equal(0, shellGrid.ColumnDefinitions[2].ActualWidth);

        shell.BookDetail.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(360, shellGrid.ColumnDefinitions[2].ActualWidth, 0);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Inspector_Width_FollowsTheShellAndClampsWhenResized()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new SeededLayoutReadModel(3));
        await shell.InitializeAsync();
        (Window window, CatalogueShellView view) = ShowShell(shell, 1440, 900);
        shell.BookDetail.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(360, view.FindControl<BookDetailView>("Inspector")!.Bounds.Width, 0);

        shell.ResizeInspector(480);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(480, view.FindControl<BookDetailView>("Inspector")!.Bounds.Width, 0);

        shell.ResizeInspector(2000);
        Assert.Equal(560, shell.InspectorWidth);
        shell.ResizeInspector(5);
        Assert.Equal(320, shell.InspectorWidth);

        await shell.CommitInspectorWidthAsync();
        Assert.Equal(320, shell.Preferences.Current.InspectorWidth);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Inspector_ResizeHandle_IsNamedAndResizesWithArrowKeys()
    {
        MainShellViewModel shell = CreateLayoutShell(new InMemoryLocalizationService(), new SeededLayoutReadModel(3));
        await shell.InitializeAsync();
        (Window window, CatalogueShellView view) = ShowShell(shell, 1440, 900);
        shell.BookDetail.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        Thumb handle = view.GetVisualDescendants().OfType<Thumb>()
            .Single(t => AutomationProperties.GetAutomationId(t) == "Detail.ResizeHandle");
        Assert.Equal("Resize the details panel", AutomationProperties.GetName(handle));
        Assert.True(handle.Focusable);

        handle.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.InspectorWidth > 360, "Left should widen a panel docked on the right.");

        for (int i = 0; i < 40; i++)
        {
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(320, shell.InspectorWidth);
        Assert.True(shell.BookDetail.IsVisible, "Arrow keys on the handle must not close or navigate.");

        window.Close();
        shell.Dispose();
    }

    private static void PressEscape(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }
}
