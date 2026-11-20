using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Startup;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.Views;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 12, slice 1: the reader focus layout (one toolbar, one navigation set, full
/// screen) and the K127 return-to-library guarantee.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    private static readonly string[] RemovedNavigationIds =
    [
        "Reader.Previous",
        "Reader.Next",
        "Reader.First",
        "Reader.Last",
        "Reader.Nav.Previous",
        "Reader.Nav.Next",
    ];

    [AvaloniaFact]
    public async Task ReaderToolbar_HasSingleNavigationSet()
    {
        (MainShellViewModel shell, Window window, CatalogueShellView view) = await ShowOpenReaderAsync(1280, 760);

        List<string> ids = AutomationIds(view);
        foreach (string removed in RemovedNavigationIds)
        {
            Assert.DoesNotContain(removed, ids);
        }

        Assert.Single(ids, id => id == "Reader.PageNumber");
        Assert.Single(ids, id => id == "Reader.BackToLibrary");
        Assert.Contains("Reader.EdgePrev", ids);
        Assert.Contains("Reader.EdgeNext", ids);

        // Exactly one toolbar: the page field lives in it and the old second row is gone.
        Border toolbar = Assert.IsType<Border>(view.GetVisualDescendants()
            .OfType<Control>().First(c => c.Name == "ReaderToolbar"));
        Control pageField = view.GetVisualDescendants().OfType<Control>()
            .First(c => AutomationProperties.GetAutomationId(c) == "Reader.PageNumber");
        Assert.True(toolbar.IsVisualAncestorOf(pageField));

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ReaderFocusMode_HidesRailAndCatalogueToolbar()
    {
        (MainShellViewModel shell, Window window, CatalogueShellView view) = await ShowOpenReaderAsync(1280, 760);

        Assert.True(shell.IsReaderFocusMode);
        Border rail = view.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("rail"));
        Border catalogueToolbar = Assert.IsType<Border>(view.FindControl<Border>("ContextualToolbar"));
        Assert.False(rail.IsEffectivelyVisible, "The navigation rail must step aside in focus mode.");
        Assert.False(catalogueToolbar.IsEffectivelyVisible, "The catalogue toolbar must step aside in focus mode.");
        Assert.Contains("Reader.BackToLibrary", AutomationIds(view));

        await shell.ReturnToLibraryAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.IsReaderFocusMode);
        Assert.True(rail.IsEffectivelyVisible);
        Assert.True(catalogueToolbar.IsEffectivelyVisible);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ReaderToolbar_BackButton_ReturnsToLibrary()
    {
        (MainShellViewModel shell, Window window, CatalogueShellView view) = await ShowOpenReaderAsync(1280, 760);

        shell.Reader!.RequestBackToLibrary();
        await WaitForAsync(() => !shell.IsReaderFocusMode);

        Assert.True(shell.IsCatalogueActive);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(1440, 900)]
    public async Task ReaderToolbar_FitsAt1280_NoClipping(int width, int height)
    {
        (MainShellViewModel shell, Window window, CatalogueShellView view) = await ShowOpenReaderAsync(width, height);

        Border toolbar = Assert.IsType<Border>(view.GetVisualDescendants()
            .OfType<Control>().First(c => c.Name == "ReaderToolbar"));
        Assert.True(toolbar.Bounds.Width >= width - 2, "The reader toolbar must span the window in focus mode.");

        List<Control> controls = toolbar.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c is Button or TextBox && c.IsEffectivelyVisible)
            .ToList();
        Assert.NotEmpty(controls);
        foreach (Control control in controls)
        {
            Point? origin = control.TranslatePoint(default, toolbar);
            Assert.True(origin.HasValue);
            Assert.True(origin.Value.X >= -0.5, $"{AutomationProperties.GetAutomationId(control)} starts left of the toolbar.");
            Assert.True(
                origin.Value.X + control.Bounds.Width <= toolbar.Bounds.Width + 0.5,
                $"{AutomationProperties.GetAutomationId(control)} is clipped on the right at {width}px.");
            Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
        }

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ReaderToolbar_EveryControlHasAnAccessibleName()
    {
        (MainShellViewModel shell, Window window, CatalogueShellView view) = await ShowOpenReaderAsync(1280, 760);

        Border toolbar = Assert.IsType<Border>(view.GetVisualDescendants()
            .OfType<Control>().First(c => c.Name == "ReaderToolbar"));
        foreach (Control control in toolbar.GetVisualDescendants().OfType<Control>()
                     .Where(c => c is Button or TextBox && c.IsEffectivelyVisible))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                $"{control.GetType().Name} {AutomationProperties.GetAutomationId(control)} lacks an accessible name.");
        }

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public void FullScreen_F11Enters_EscRestores()
    {
        var localization = new InMemoryLocalizationService();
        var window = new DesktopShellWindow
        {
            DataContext = StartupShellViewModel.CreateBootstrap(localization),
            WindowState = WindowState.Normal,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.F11, RawInputModifiers.None, PhysicalKey.F11, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.IsFullScreen);
        Assert.Equal(WindowState.FullScreen, window.WindowState);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsFullScreen);
        Assert.Equal(WindowState.Normal, window.WindowState);

        // F11 toggles back out as well.
        window.KeyPress(Key.F11, RawInputModifiers.None, PhysicalKey.F11, null);
        window.KeyPress(Key.F11, RawInputModifiers.None, PhysicalKey.F11, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsFullScreen);
        window.Close();
    }

    [AvaloniaFact]
    public void FullScreen_Esc_ClosesPaletteThenSheetBeforeFullScreen()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = SkeletonRenderTests.CreateViewModel(localization);
        var startup = new StartupShellViewModel(
            new UnusedStartupCoordinator(),
            shell,
            new OgmaRuntimeOptions { DataDirectory = Path.GetTempPath(), LibraryRoot = Path.GetTempPath() },
            localization);
        var window = new DesktopShellWindow { DataContext = startup, WindowState = WindowState.Normal };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.ToggleFullScreen();
        shell.OpenCommandPalette();
        Dispatcher.UIThread.RunJobs();

        // The palette and the sheet are mutually exclusive layers; each closes before full screen.
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.IsCommandPaletteOpen);
        Assert.True(window.IsFullScreen);

        shell.IsShortcutSheetOpen = true;
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.IsShortcutSheetOpen);
        Assert.True(window.IsFullScreen);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsFullScreen);
        Assert.Equal(WindowState.Normal, window.WindowState);
        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ReturnToLibrary_WhenCloseFails_StillOpensCatalogue()
    {
        // K127: a failed or cancelled close must never leave the user stranded in the reader.
        var localization = new InMemoryLocalizationService();
        var session = new FakeReaderSessionService();
        MainShellViewModel shell = CreateReaderShell(localization, session);
        await shell.OpenReaderAsync("book-001", pageHint: 1, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsReaderFocusMode);

        session.CloseFailure = new IOException("flush failed");
        await shell.ReturnToLibraryAsync();
        Assert.True(shell.IsCatalogueActive);
        Assert.False(shell.IsReaderActive);

        await shell.OpenReaderAsync("book-001", pageHint: 1, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsReaderFocusMode);

        session.CloseFailure = new OperationCanceledException();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => shell.ReturnToLibraryAsync());
        Assert.True(shell.IsCatalogueActive);
        Assert.False(shell.IsReaderActive);
        shell.Dispose();
    }

    private static MainShellViewModel CreateReaderShell(
        InMemoryLocalizationService localization,
        FakeReaderSessionService session)
    {
        var readModel = new EmptyCatalogueReadModel();
        var filter = new CatalogueFilterViewModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization);
        var shelfSidebar = new ShelfSidebarViewModel(readModel, new NoOpCatalogueWriteService(), localization, filter);
        var reader = new ReaderViewModel(
            session,
            new EmptyAnnotationService(),
            new EmptyBookmarkService(),
            new EmptyLayerService(),
            new EmptyCitationService(),
            new EmptyReadingMemoryService(),
            localization);
        return new MainShellViewModel(localization, catalogue, bookDetail, shelfSidebar, reader);
    }

    private static async Task<(MainShellViewModel Shell, Window Window, CatalogueShellView View)> ShowOpenReaderAsync(
        int width,
        int height)
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = CreateReaderShell(localization, new FakeReaderSessionService());
        await shell.OpenReaderAsync("book-001", pageHint: 4, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();

        var view = new CatalogueShellView { DataContext = shell };
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (shell, window, view);
    }

    private static List<string> AutomationIds(Visual root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList()!;

    private sealed class UnusedStartupCoordinator : IApplicationStartupCoordinator
    {
        public Task<ApplicationStartupReport> InitializeAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
