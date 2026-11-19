using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Converters;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 10, slice 1: composed accessible names on item containers (K14), the
/// virtualised uniform grid, the optional pager ("Show all") and the cover-unavailable badge.
/// </summary>
public sealed class Sept23Phase10GridTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Initializes the test class.</summary>
    public Sept23Phase10GridTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ComposeName_IsTitleByAuthor_WithLocalisedFallbackAndStatusBadges()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        BookSummaryProjection plain = Book("a", "The Lantern Keeper", "Ada Byron");
        Assert.Equal("The Lantern Keeper, by Ada Byron", BookAccessibleNameConverter.ComposeName(plain, localization));

        BookSummaryProjection authorless = Book("b", "Anonymous Notes", author: null);
        Assert.Equal("Anonymous Notes, by Unknown author", BookAccessibleNameConverter.ComposeName(authorless, localization));
        Assert.Equal("Unknown author", BookAccessibleNameConverter.ComposeAuthor(authorless, localization));

        BookSummaryProjection busy = Book("c", "Atlas", "Mercator") with
        {
            IsLocked = true,
            IsAvailable = false,
            IsFavourite = true,
            CopyCount = 2,
            Processing = Processing(BookTextStatus.Searchable),
        };
        Assert.Equal(
            "Atlas, by Mercator, Locked, Unavailable, Favourite, Searchable, 2 copies",
            BookAccessibleNameConverter.ComposeName(busy, localization));

        BookSummaryProjection untitled = Book("d", title: null, "Someone");
        Assert.Equal("Untitled, by Someone", BookAccessibleNameConverter.ComposeName(untitled, localization));

        localization.SetCulture("fr");
        Assert.Equal("Anonymous Notes, par Auteur inconnu", BookAccessibleNameConverter.ComposeName(authorless, localization));
        Assert.Equal(
            "Atlas, par Mercator, Verrouillé, Indisponible, Favori, Recherchable, 2 exemplaires",
            BookAccessibleNameConverter.ComposeName(busy, localization));
    }

    [AvaloniaFact]
    public async Task ItemNames_AreComposed_AndNeverRecordDumps_InGridListAndDirectory()
    {
        List<BookSummaryProjection> books =
        [
            Book("a", "Alpha Atlas", "Ann Author"),
            Book("b", "Bravo Book", author: null),
            Book("c", "Charlie Codex", "Cy Writer") with { IsLocked = true },
        ];
        using CatalogueViewModel viewModel = await LoadAsync(books);

        foreach (Func<UserControl> create in new Func<UserControl>[]
                 {
                     () => new CatalogueGridView(),
                     () => new CatalogueListView(),
                     () => new CatalogueDirectoryView(),
                 })
        {
            UserControl view = create();
            view.DataContext = viewModel;
            Window window = Show(view, 1000, 700);

            List<ListBoxItem> items = MainItems(window).ToList();
            Assert.Equal(3, items.Count);
            foreach (ListBoxItem item in items)
            {
                var book = (BookSummaryProjection)item.DataContext!;
                string? name = AutomationProperties.GetName(item);
                Assert.NotNull(name);
                Assert.DoesNotContain("Projection {", name, StringComparison.Ordinal);
                Assert.StartsWith($"{book.Title}, by ", name, StringComparison.Ordinal);
                Assert.Equal($"Catalogue.Item.{book.BookId}", AutomationProperties.GetAutomationId(item));
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(item)));

                // The duplicate inner name is gone: only the container is announced.
                Assert.DoesNotContain(
                    item.GetVisualDescendants().OfType<Border>(),
                    border => AutomationProperties.GetName(border) == book.Title);
            }

            Assert.Equal("Bravo Book, by Unknown author", AutomationProperties.GetName(items[1]));
            Assert.Equal("Charlie Codex, by Cy Writer, Locked", AutomationProperties.GetName(items[2]));
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Grid_With2000Books_RealisesFewerThan200Containers()
    {
        using CatalogueViewModel viewModel = await LoadAsync(ManyBooks(2000));
        Assert.Equal(2000, viewModel.FilteredItems.Count);

        Window window = Show(new CatalogueGridView { DataContext = viewModel }, 1280, 800);

        Assert.NotNull(window.GetVisualDescendants().OfType<VirtualizingUniformGridPanel>().SingleOrDefault());
        int realised = MainItems(window).Count();
        _output.WriteLine($"realised containers at 2,000 books: {realised}");
        Assert.InRange(realised, 1, 199);

        // After scrolling, recycled (hidden) containers hold no stale book, so UI Automation can
        // never find or select a book through a container that no longer shows it.
        ScrollViewer scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
            .First(s => s.GetVisualDescendants().OfType<VirtualizingUniformGridPanel>().Any());
        // Varied jumps and a resize exercise both the same-pass reuse and the cleared pool.
        foreach ((double fraction, double width) in new[] { (0.5, 1280.0), (0.0, 900.0), (1.0, 1280.0), (0.33, 700.0), (0.0, 1280.0) })
        {
            window.Width = width;
            scroller.Offset = scroller.Offset.WithY(scroller.Extent.Height * fraction);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.All(
                window.GetVisualDescendants().OfType<ListBoxItem>().Where(item => !item.IsVisible),
                item =>
                {
                    Assert.Null(item.Content);
                    Assert.IsNotType<BookSummaryProjection>(item.DataContext);
                    Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(item)));
                });
            Assert.All(
                MainItems(window).Where(item => item.IsVisible),
                item =>
                {
                    Assert.Same(item.DataContext, item.Content);
                    BookSummaryProjection book = Assert.IsType<BookSummaryProjection>(item.DataContext);
                    Assert.StartsWith(book.Title!, AutomationProperties.GetName(item), StringComparison.Ordinal);
                    Assert.Equal($"Catalogue.Item.{book.BookId}", AutomationProperties.GetAutomationId(item));
                });
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task Grid_KeyboardNavigation_ReachesLastItem_AndBack()
    {
        using CatalogueViewModel viewModel = await LoadAsync(ManyBooks(600));
        Window window = Show(new CatalogueGridView { DataContext = viewModel }, 1280, 800);

        ListBoxItem first = MainItems(window).First();
        first.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("book-0001", FocusedBookId(window));

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        int columns = window.GetVisualDescendants().OfType<VirtualizingUniformGridPanel>().Single().Columns;
        Assert.True(columns > 1);
        Assert.Equal($"book-{1 + columns:D4}", FocusedBookId(window));

        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(string.CompareOrdinal(FocusedBookId(window), $"book-{1 + columns:D4}") > 0);

        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("book-0599", FocusedBookId(window));

        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("book-0000", FocusedBookId(window));
        window.Close();
    }

    [AvaloniaFact]
    public async Task ShowAll_IsOnByDefault_HidesThePager_AndPersists()
    {
        var store = new MemoryViewStateStore();
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        using var viewModel = new CatalogueViewModel(
            new SeededReadModel(ManyBooks(205)),
            new NoOpNavigation(),
            localization,
            viewStateStore: store);
        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.ShowAll);
        Assert.False(viewModel.IsPaged);
        Assert.Equal(205, viewModel.FilteredItems.Count);

        viewModel.ShowAll = false;
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsPaged);
        Assert.Equal(100, viewModel.FilteredItems.Count);

        await Task.Delay(350);
        Assert.NotNull(store.State);
        Assert.False(store.State!.ShowAll);

        viewModel.ShowAll = true;
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsPaged);
        Assert.Equal(205, viewModel.FilteredItems.Count);
    }

    [AvaloniaFact]
    public async Task CoverBadge_IsVisible_ForAFailedCover_WithTheReason()
    {
        BookSummaryProjection failed = Book("f", "Failed Cover", "Ann Author") with
        {
            HasFailedCover = true,
            Processing = Processing(BookTextStatus.Searchable),
        };
        BookSummaryProjection extracted = Book("e", "Extracted Cover", "Ann Author") with
        {
            CoverRelativePath = "covers/e.png",
            CoverSource = "embedded",
            Processing = Processing(BookTextStatus.Searchable),
        };
        BookSummaryProjection pending = Book("p", "Pending Cover", "Ann Author");
        Assert.True(failed.IsCoverUnavailable);
        Assert.Equal(CoverUnavailableReason.ExtractionFailed, failed.CoverUnavailableReason);
        Assert.False(extracted.IsCoverUnavailable);
        Assert.False(pending.IsCoverUnavailable);

        using CatalogueViewModel viewModel = await LoadAsync([extracted, failed, pending]);
        Window window = Show(new CatalogueGridView { DataContext = viewModel }, 1000, 700);

        Dictionary<string, Control?> badges = MainItems(window).ToDictionary(
            item => ((BookSummaryProjection)item.DataContext!).BookId,
            item => item.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == "Catalogue.Item.Badge.CoverUnavailable"));

        Control failedBadge = Assert.IsAssignableFrom<Control>(badges["f"]);
        Assert.True(failedBadge.IsEffectivelyVisible);
        Assert.Equal("The cover could not be extracted from this PDF.", ToolTip.GetTip(failedBadge));
        Assert.False(badges["p"]?.IsEffectivelyVisible ?? false);
        window.Close();
    }

    [Fact]
    public void VirtualisationSwitch_ZeroSelectsWrapPanel()
    {
        Assert.True(CatalogueGridView.IsVirtualizationEnabled(null));
        Assert.True(CatalogueGridView.IsVirtualizationEnabled("1"));
        Assert.False(CatalogueGridView.IsVirtualizationEnabled("0"));
    }

    [AvaloniaFact]
    [Trait("Category", "Benchmark")]
    public async Task Grid_ScrollThrough2000Books_LayoutStaysWithinFrameBudget()
    {
        using CatalogueViewModel viewModel = await LoadAsync(ManyBooks(2000));
        Window window = Show(new CatalogueGridView { DataContext = viewModel }, 1280, 800);
        ScrollViewer scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
            .First(s => s.GetVisualDescendants().OfType<VirtualizingUniformGridPanel>().Any());

        var frames = new List<double>();
        double step = scroller.Viewport.Height / 3;
        while (scroller.Offset.Y + scroller.Viewport.Height < scroller.Extent.Height - 1)
        {
            long start = Stopwatch.GetTimestamp();
            scroller.Offset = scroller.Offset.WithY(scroller.Offset.Y + step);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            frames.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        frames.Sort();
        double p95 = frames[(int)(frames.Count * 0.95)];
        double max = frames[^1];
        _output.WriteLine($"scroll steps {frames.Count}; layout p95 {p95:F2} ms; max {max:F2} ms; realised {MainItems(window).Count()}");
        double median = frames[frames.Count / 2];
        _output.WriteLine($"layout median {median:F2} ms");
        Assert.True(median < 50, $"the median scroll step took {median:F1} ms");
        window.Close();
    }

    private static IEnumerable<ListBoxItem> MainItems(Window window) =>
        window.GetVisualDescendants().OfType<ListBoxItem>()
            .Where(item => AutomationProperties.GetAutomationId(item)?.StartsWith("Catalogue.Item.", StringComparison.Ordinal) == true);

    private static string? FocusedBookId(Window window) =>
        (window.FocusManager?.GetFocusedElement() as Control)?.DataContext is BookSummaryProjection book
            ? book.BookId
            : null;

    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task<CatalogueViewModel> LoadAsync(IReadOnlyList<BookSummaryProjection> books)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new CatalogueViewModel(new SeededReadModel(books), new NoOpNavigation(), localization);
        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        return viewModel;
    }

    private static List<BookSummaryProjection> ManyBooks(int count) =>
        Enumerable.Range(0, count).Select(i => Book($"book-{i:D4}", $"Book {i:D4}", $"Author {i % 37}")).ToList();

    private static BookSummaryProjection Book(string id, string? title, string? author) =>
        new(id, title, author is null ? [] : [author], null, 0, null, [], null, true, 2020);

    private static CatalogueProcessingProjection Processing(BookTextStatus status) =>
        new(SearchBookIndexStatus.Indexed, SearchEmbeddingStatus.NotEmbedded, 0, false, status);

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class MemoryViewStateStore : ICatalogueViewStateStore
    {
        public CatalogueViewState? State { get; private set; }

        public Task<CatalogueViewState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task SaveAsync(CatalogueViewState state, CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class SeededReadModel(IReadOnlyList<BookSummaryProjection> books) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in books)
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
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
