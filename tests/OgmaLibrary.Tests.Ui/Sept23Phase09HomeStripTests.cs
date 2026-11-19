using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Settings;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 09 headless proof for the library home strip, the in-place card sync that keeps
/// unchanged cards stable across background refreshes, and the vector QR module matrix.
/// </summary>
public sealed class Sept23Phase09HomeStripTests
{
    [AvaloniaFact]
    public async Task HomeStrip_PutsInProgressBooksFirst_FurthestFirst_CappedAtStripSize()
    {
        // 12 books in progress (more than the strip holds) plus finished and unstarted books.
        var books = new List<BookSummaryProjection>();
        for (int i = 0; i < 12; i++)
        {
            books.Add(Book($"p{i:D2}", $"Progress {i:D2}", progress: 10 + i));
        }

        books.Add(Book("done", "Aardvark finished", progress: 100));
        books.Add(Book("new", "Aardvark unstarted", progress: 0));
        books.Add(Book("none", "Aardvark no progress", progress: null));

        var localization = new InMemoryLocalizationService();
        using var viewModel = await LoadAsync(new MutableReadModel(books), localization);

        Assert.True(viewModel.HasHomeStrip);
        Assert.True(viewModel.HasBooksInProgress);
        Assert.Equal(CatalogueViewModel.HomeStripSize, viewModel.HomeStripItems.Count);
        Assert.All(viewModel.HomeStripItems, b => Assert.InRange(b.ReadingProgressPct!.Value, 0.01, 99.99));
        Assert.Equal(
            Enumerable.Range(4, 8).Reverse().Select(i => $"p{i:D2}"),
            viewModel.HomeStripItems.Select(b => b.BookId));

        Assert.Equal("Continue reading", viewModel.HomeStripTitle);
        Assert.Equal("15 books in your library", viewModel.HomeSummaryText);

        localization.SetCulture("fr");
        Assert.Equal("Reprendre la lecture", viewModel.HomeStripTitle);
        Assert.Equal("15 livres dans votre bibliothèque", viewModel.HomeSummaryText);
    }

    [AvaloniaFact]
    public async Task HomeStrip_FallsBackToShelvedBooksByTitle_WhenNothingIsInProgress()
    {
        var books = Enumerable.Range(0, 10)
            .Select(i => Book($"b{i:D2}", $"Shelved {9 - i:D2}", progress: i % 2 == 0 ? 0 : 100))
            .ToList();

        var localization = new InMemoryLocalizationService();
        using var viewModel = await LoadAsync(new MutableReadModel(books), localization);

        Assert.False(viewModel.HasBooksInProgress);
        Assert.Equal(CatalogueViewModel.HomeStripSize, viewModel.HomeStripItems.Count);
        Assert.Equal(
            Enumerable.Range(0, 8).Select(i => $"Shelved {i:D2}"),
            viewModel.HomeStripItems.Select(b => b.Title));

        Assert.Equal("On your shelves", viewModel.HomeStripTitle);
        Assert.Equal("10 books in your library", viewModel.HomeSummaryText);

        localization.SetCulture("fr");
        Assert.Equal("Sur vos étagères", viewModel.HomeStripTitle);
        Assert.Equal("10 livres dans votre bibliothèque", viewModel.HomeSummaryText);
    }

    [AvaloniaFact]
    public async Task HomeStrip_IsHidden_WhenTheLibraryIsEmpty()
    {
        using var viewModel = await LoadAsync(new MutableReadModel([]), new InMemoryLocalizationService());

        Assert.False(viewModel.HasHomeStrip);
        Assert.Empty(viewModel.HomeStripItems);
    }

    [AvaloniaFact]
    public async Task Refresh_KeepsUnchangedCardInstances_ReplacesChangedCards_RemovesSurplus()
    {
        var readModel = new MutableReadModel(
        [
            Book("a", "Alpha", progress: 0),
            Book("b", "Bravo", progress: 0),
            Book("c", "Charlie", progress: 0),
            Book("d", "Delta", progress: 0),
        ]);
        using var viewModel = await LoadAsync(readModel, new InMemoryLocalizationService());
        Assert.Equal(["a", "b", "c", "d"], viewModel.FilteredItems.Select(b => b.BookId));
        BookSummaryProjection alphaCard = viewModel.FilteredItems[0];
        BookSummaryProjection bravoCard = viewModel.FilteredItems[1];

        int replaced = 0;
        int removed = 0;
        viewModel.FilteredItems.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                replaced++;
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                removed++;
            }
            else
            {
                Assert.Fail($"Unexpected collection change {e.Action}; a refresh must not reset the list.");
            }
        };

        // Fresh, value-equal projections for a and b (new instances, same card fields); c changes
        // its rating; d disappears from the library.
        readModel.Books =
        [
            Book("a", "Alpha", progress: 0),
            Book("b", "Bravo", progress: 0),
            Book("c", "Charlie", progress: 0) with { Rating = 5 },
        ];
        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["a", "b", "c"], viewModel.FilteredItems.Select(b => b.BookId));
        Assert.Same(alphaCard, viewModel.FilteredItems[0]);
        Assert.Same(bravoCard, viewModel.FilteredItems[1]);
        Assert.Equal(5, viewModel.FilteredItems[2].Rating);
        Assert.Equal(1, replaced);
        Assert.Equal(1, removed);
    }

    [AvaloniaFact]
    public async Task Refresh_ReplacesCard_WhenAnyDisplayedFieldChanges()
    {
        BookSummaryProjection original = Book("a", "Alpha", progress: 10);
        BookSummaryProjection[] variants =
        [
            original with { Title = "Alpha 2" },
            original with { CoverRelativePath = "covers/a.png" },
            original with { Status = 2 },
            original with { Rating = 5 },
            original with { ReadingProgressPct = 20 },
            original with { IsAvailable = false },
            original with { Year = 1999 },
            original with { IsFavourite = true },
            original with { IsLocked = true },
            original with { RelativePath = "x/a.pdf" },
            original with { Authors = ["Someone Else"] },
            original with { ShelfIds = ["shelf-1"] },
        ];

        foreach (BookSummaryProjection variant in variants)
        {
            var readModel = new MutableReadModel([original]);
            using var viewModel = await LoadAsync(readModel, new InMemoryLocalizationService());
            BookSummaryProjection before = viewModel.FilteredItems[0];

            readModel.Books = [variant];
            await viewModel.LoadAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(before, viewModel.FilteredItems[0]);
            Assert.Same(variant, viewModel.FilteredItems[0]);
            Assert.Same(variant, viewModel.HomeStripItems[0]);
        }
    }

    [Fact]
    public void QrBuildModules_ReturnsNull_ForNullOrEmptyPayload()
    {
        Assert.Null(QrCodeView.BuildModules(null));
        Assert.Null(QrCodeView.BuildModules(string.Empty));
        Assert.Null(QrCodeView.BuildModules("   "));
    }

    [Fact]
    public void QrBuildModules_IsSquare_HasALightQuietZone_AndDarkModules()
    {
        bool[,]? modules = QrCodeView.BuildModules("ogma://join?host=192.0.2.10&port=5055&code=TEST-0000");

        Assert.NotNull(modules);
        int side = modules!.GetLength(0);
        Assert.Equal(side, modules.GetLength(1));
        Assert.True(side >= 21 + (2 * QrCodeView.QuietZoneModules), $"side {side} is smaller than a version-1 symbol with its quiet zone");

        int quiet = QrCodeView.QuietZoneModules;
        int dark = 0;
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                bool inQuietZone = y < quiet || x < quiet || y >= side - quiet || x >= side - quiet;
                if (inQuietZone)
                {
                    Assert.False(modules[y, x], $"quiet-zone module ({x},{y}) is dark");
                }
                else if (modules[y, x])
                {
                    dark++;
                }
            }
        }

        Assert.True(dark > 0, "the symbol has no dark modules");
    }

    [AvaloniaFact]
    public void QrView_MeasuresAtLeastFourPixelsPerModule_AndNothingForAnEmptyPayload()
    {
        Assert.True(QrCodeView.MinimumModuleSize >= 4);

        var view = new QrCodeView { Payload = "ogma://join?code=TEST-0000" };
        view.Measure(Size.Infinity);
        Assert.True(view.ModuleCount > 0);
        Assert.Equal(view.ModuleCount * 4.0, view.DesiredSize.Width, 3);
        Assert.Equal(view.DesiredSize.Width, view.DesiredSize.Height, 3);

        view.Payload = null;
        view.Measure(Size.Infinity);
        Assert.Equal(0, view.ModuleCount);
        Assert.Equal(new Size(0, 0), view.DesiredSize);
    }

    [Fact]
    public void ComponentGalleryGesture_IsCompiledIntoDebugBuildsOnly()
    {
        System.Reflection.Assembly app = typeof(OgmaLibrary.App.Views.DesktopShellWindow).Assembly;
        string? configuration = app
            .GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
            .OfType<System.Reflection.AssemblyConfigurationAttribute>()
            .SingleOrDefault()?.Configuration;
        bool hasGesture = typeof(OgmaLibrary.App.Views.DesktopShellWindow).GetMethod(
            "GalleryGesture_KeyDown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null;

        Assert.Equal(string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase), hasGesture);
    }

    private static async Task<CatalogueViewModel> LoadAsync(
        MutableReadModel readModel,
        InMemoryLocalizationService localization)
    {
        localization.SetCulture("en");
        var viewModel = new CatalogueViewModel(readModel, new NoOpNavigation(), localization);
        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        return viewModel;
    }

    private static BookSummaryProjection Book(string id, string title, double? progress) =>
        new(id, title, ["Synthetic Author"], null, 0, 3, [], progress, true, 2020);

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class MutableReadModel : ICatalogueReadModel
    {
        public MutableReadModel(IReadOnlyList<BookSummaryProjection> books) => Books = books;

        public IReadOnlyList<BookSummaryProjection> Books { get; set; }

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in Books)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return book;
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
