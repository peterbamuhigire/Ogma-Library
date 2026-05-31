using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Headless UI tests for the catalogue grid view (FR-CAT-001).
/// Seeds ~24 synthetic books, renders the grid, captures a screenshot, and asserts
/// the view model exposes the seeded count.
/// </summary>
public sealed class CatalogueGridTests
{
    private static readonly string ArtifactsDir;
    private static readonly string DocsImagesDir;

    static CatalogueGridTests()
    {
        // tests/OgmaLibrary.Tests.Ui/bin/Debug/net10.0 -> repo root
        string baseDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "screenshots");
        Directory.CreateDirectory(baseDir);
        ArtifactsDir = Path.GetFullPath(baseDir);

        string docsDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "developer-guide", "images");
        Directory.CreateDirectory(docsDir);
        DocsImagesDir = Path.GetFullPath(docsDir);
    }

    private const int SeedCount = 24;

    /// <summary>
    /// Seeds 24 synthetic books into an in-memory CatalogueViewModel,
    /// renders the CatalogueGridView, captures a screenshot, and asserts
    /// the grid view model shows all 24 books.
    /// </summary>
    [AvaloniaFact]
    public void CatalogueGridView_PopulatedWith24Books_CapturesScreenshot()
    {
        // Arrange: create 24 synthetic book summaries.
        var books = CreateSyntheticBooks(SeedCount);
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        var readModel = new SeededCatalogueReadModel(books);
        var nav = new NullNavigation();
        var vm = new CatalogueViewModel(readModel, nav, localization);

        // Load books synchronously (headless, so we can drive the dispatcher).
        var loadTask = vm.LoadAsync(CancellationToken.None);
        Dispatcher.UIThread.RunJobs();

        // Wait for async load to complete.
        loadTask.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        // Assert view model shows all seeded books.
        Assert.Equal(SeedCount, vm.TotalCount);
        Assert.Equal(SeedCount, vm.FilteredCount);
        Assert.False(vm.IsEmpty);

        // Act: render the grid view in a window.
        var gridView = new CatalogueGridView { DataContext = vm };
        var window = new Window
        {
            Width = 1000,
            Height = 680,
            Content = gridView,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Capture screenshot.
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        string screenshotPath = Path.Combine(ArtifactsDir, "catalogue-grid-en.png");
        frame!.Save(screenshotPath);

        // Also copy to docs.
        string docsPath = Path.Combine(DocsImagesDir, "catalogue-grid-en.png");
        frame.Save(docsPath);
    }

    /// <summary>
    /// Seeds 24 books and renders the list view, captures a screenshot.
    /// </summary>
    [AvaloniaFact]
    public void CatalogueListView_PopulatedWith24Books_CapturesScreenshot()
    {
        var books = CreateSyntheticBooks(SeedCount);
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        var readModel = new SeededCatalogueReadModel(books);
        var nav = new NullNavigation();
        var vm = new CatalogueViewModel(readModel, nav, localization);
        vm.CurrentView = CatalogueView.List;

        var loadTask = vm.LoadAsync(CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        loadTask.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SeedCount, vm.TotalCount);

        var listView = new CatalogueListView { DataContext = vm };
        var window = new Window
        {
            Width = 1000,
            Height = 680,
            Content = listView,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(ArtifactsDir, "catalogue-list-en.png"));
    }

    // ── Synthetic data ────────────────────────────────────────────────────────

    private static List<BookSummaryProjection> CreateSyntheticBooks(int count)
    {
        var corpus = OgmaLibrary.Tests.GoldenCorpus.SyntheticCorpusGenerator.Generate(count, seed: 42);
        return corpus.Select((b, i) => new BookSummaryProjection(
            BookId: $"book-{i:D3}",
            Title: b.Title,
            Authors: new[] { b.Author },
            CoverRelativePath: null,
            Status: 0,
            Rating: (i % 5) + 1,
            ShelfIds: Array.Empty<string>(),
            ReadingProgressPct: i * 4.0,
            IsAvailable: true,
            Year: b.Year)).ToList();
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class SeededCatalogueReadModel : ICatalogueReadModel
    {
        private readonly IReadOnlyList<BookSummaryProjection> _books;

        public SeededCatalogueReadModel(IReadOnlyList<BookSummaryProjection> books)
            => _books = books;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (var b in _books)
            {
                yield return b;
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

    private sealed class NullNavigation :
        IBookDetailNavigationService,
        IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
