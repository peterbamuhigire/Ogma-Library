using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for Phase 19 bounded catalogue paging and state restore.</summary>
public sealed class Phase19CataloguePagingTests
{
    [AvaloniaFact]
    public async Task Catalogue_PagesResultsAndRestoresNonSensitiveViewState()
    {
        var store = new MemoryViewStateStore
        {
            State = new CatalogueViewState(
                View: nameof(CatalogueView.List),
                TitleSearch: "Topic",
                AuthorSearch: null,
                StatusFilter: null,
                MinRating: null,
                MaxRating: null,
                AvailabilityFilter: true,
                SelectedShelfId: null,
                SortField: nameof(CatalogueSortField.Year),
                SortAscending: false,
                CurrentPage: 2),
        };
        var viewModel = new CatalogueViewModel(
            new SeededReadModel(CreateBooks(205)),
            new NoOpNavigation(),
            new InMemoryLocalizationService(),
            viewStateStore: store);

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CatalogueView.List, viewModel.CurrentView);
        Assert.Equal(205, viewModel.TotalFilteredCount);
        Assert.Equal(3, viewModel.TotalPages);
        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(100, viewModel.FilteredItems.Count);
        Assert.True(viewModel.CanGoToPreviousPage);
        Assert.True(viewModel.CanGoToNextPage);

        viewModel.GoToNextPage();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, viewModel.CurrentPage);
        Assert.Equal(5, viewModel.FilteredItems.Count);

        viewModel.GoToPreviousPage();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(100, viewModel.FilteredItems.Count);

        viewModel.Filter.TitleSearch = "Topic 204";
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(350);
        Assert.NotNull(store.State);
        Assert.Equal("Topic 204", store.State!.TitleSearch);
        Assert.Equal(1, store.State.CurrentPage);
        viewModel.Dispose();
    }

    private static List<BookSummaryProjection> CreateBooks(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new BookSummaryProjection(
                $"book-{index:D3}",
                $"Topic {index:D3}",
                [$"Author {index:D3}"],
                null,
                0,
                (index % 5) + 1,
                [],
                null,
                true,
                2000 + index))
            .ToList();

    private sealed class MemoryViewStateStore : ICatalogueViewStateStore
    {
        public CatalogueViewState? State { get; set; }

        public Task<CatalogueViewState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task SaveAsync(CatalogueViewState state, CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SeededReadModel : ICatalogueReadModel
    {
        private readonly IReadOnlyList<BookSummaryProjection> _books;

        public SeededReadModel(IReadOnlyList<BookSummaryProjection> books) => _books = books;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in _books)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return book;
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
