using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Layout regression tests for the catalogue shell (Sept-23 Kaizen K05/K10). From 2026-09-04
/// to 2026-09-25 the paging bar was nested inside the catalogue content cell and painted an
/// opaque panel over every catalogue view and the first-run empty state, while view-model
/// tests stayed green. These tests assert that content is not covered by later-painted,
/// opaque elements.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(1920, 1080)]
    public async Task CatalogueShellLayout_EmptyStateIsNotCoveredByOpaqueElements(int width, int height)
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new EmptyCatalogueReadModel();
        MainShellViewModel shell = CreateLayoutShell(localization, readModel);
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        (Window window, CatalogueShellView view) = ShowShell(shell, width, height);

        TextBlock heading = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == shell.EmptyStateHeading && block.IsEffectivelyVisible);
        AssertNotCovered(view, heading);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(1920, 1080)]
    public async Task CatalogueShellLayout_BookCardsAreNotCoveredByPagerOrPanels(int width, int height)
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new SeededLayoutReadModel(17);
        MainShellViewModel shell = CreateLayoutShell(localization, readModel);
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        (Window window, CatalogueShellView view) = ShowShell(shell, width, height);

        List<ListBoxItem> cards = view.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Where(item => item.IsEffectivelyVisible && item.DataContext is BookSummaryProjection)
            .ToList();
        Assert.NotEmpty(cards);
        AssertNotCovered(view, cards[0]);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaTheory]
    [InlineData(0, false)]
    [InlineData(17, false)]
    [InlineData(101, true)]
    public async Task CatalogueShellLayout_PagerShownOnlyWhenThereIsMoreThanOnePage(int books, bool expected)
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = CreateLayoutShell(localization, new SeededLayoutReadModel(books));
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(expected, shell.IsPagerVisible);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task CatalogueShellStatus_IdleWithBooks_ReportsBookCountNotChooseFolder()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = CreateLayoutShell(localization, new SeededLayoutReadModel(17));
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("17 books in your library", shell.StatusText);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task CatalogueShellStatus_EmptyLibrary_KeepsChooseFolderPrompt()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel shell = CreateLayoutShell(localization, new EmptyCatalogueReadModel());
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(localization["MainWindow.Status.Ready"], shell.StatusText);
        shell.Dispose();
    }

    [AvaloniaFact]
    public void CatalogueShellStatus_ScanningNeverReportsMoreProcessedThanDiscovered()
    {
        var localization = new InMemoryLocalizationService();
        var progress = new OgmaLibrary.Infrastructure.Ingestion.ScanProgressService();
        MainShellViewModel shell = CreateLayoutShell(localization, new EmptyCatalogueReadModel(), progress);

        progress.SetPhase(OgmaLibrary.Application.Ingestion.ScanPhase.Processing);
        for (int i = 0; i < 17; i++)
        {
            progress.IncrementDiscovered();
        }

        // Asset jobs historically incremented the same counter (K13: "59 / 17 files").
        for (int i = 0; i < 59; i++)
        {
            progress.IncrementCompleted();
        }

        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Scanning: 17 of 17 files", shell.StatusText);

        progress.SetPhase(OgmaLibrary.Application.Ingestion.ScanPhase.GeneratingAssets);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(localization["Scan.Progress.PreparingBooks"], shell.StatusText);
        Assert.DoesNotContain("59", shell.StatusText, StringComparison.Ordinal);
        shell.Dispose();
    }

    private static MainShellViewModel CreateLayoutShell(
        InMemoryLocalizationService localization,
        ICatalogueReadModel readModel,
        OgmaLibrary.Application.Ingestion.IScanProgressService? scanProgress = null)
    {
        var filter = new CatalogueFilterViewModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization);
        var shelfSidebar = new ShelfSidebarViewModel(readModel, new NoOpCatalogueWriteService(), localization, filter);
        return new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            scanProgress: scanProgress);
    }

    private static (Window Window, CatalogueShellView View) ShowShell(MainShellViewModel shell, int width, int height)
    {
        var view = new CatalogueShellView { DataContext = shell };
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    /// <summary>
    /// Fails when a visible element drawn after <paramref name="target"/> (later in visual-tree
    /// order, and not one of its descendants) has an opaque background covering the target's centre.
    /// </summary>
    private static void AssertNotCovered(Visual root, Visual target)
    {
        Point? centre = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root);
        Assert.True(centre.HasValue, "Target is not in the root's visual tree.");

        List<Visual> paintOrder = root.GetVisualDescendants().ToList();
        int targetIndex = paintOrder.IndexOf(target);
        Assert.True(targetIndex >= 0, "Target is not a descendant of the root.");

        var coverers = new List<string>();
        foreach (Visual candidate in paintOrder.Skip(targetIndex + 1))
        {
            if (candidate.IsVisualAncestorOf(target) || target.IsVisualAncestorOf(candidate) ||
                !candidate.IsEffectivelyVisible || !HasOpaqueBackground(candidate))
            {
                continue;
            }

            Point? origin = candidate.TranslatePoint(default, root);
            if (origin is null)
            {
                continue;
            }

            var bounds = new Rect(origin.Value, candidate.Bounds.Size);
            if (bounds.Contains(centre.Value))
            {
                coverers.Add($"{candidate.GetType().Name} at {bounds}");
            }
        }

        Assert.True(
            coverers.Count == 0,
            $"{target.GetType().Name} at {centre} is covered by: {string.Join("; ", coverers)}");
    }

    private static bool HasOpaqueBackground(Visual visual)
    {
        IBrush? brush = visual switch
        {
            Border border => border.Background,
            Panel panel => panel.Background,
            TemplatedControl control => control.Background,
            _ => null,
        };

        return brush switch
        {
            null => false,
            ISolidColorBrush solid => solid.Color.A > 0 && solid.Opacity > 0,
            _ => brush.Opacity > 0,
        };
    }

    private sealed class SeededLayoutReadModel(int count) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            for (int i = 0; i < count; i++)
            {
                yield return new BookSummaryProjection(
                    BookId: $"layout-{i:D2}",
                    Title: $"Layout Book {i:D2}",
                    Authors: ["Test Author"],
                    CoverRelativePath: null,
                    Status: 0,
                    Rating: null,
                    ShelfIds: [],
                    ReadingProgressPct: null,
                    IsAvailable: true,
                    Year: 2026);
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
