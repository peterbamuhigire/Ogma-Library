using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 10 (task 10.4): the persisted catalogue view state is applied once per view-model
/// lifetime, so a background refresh never overwrites a live filter change, and the new criteria
/// are saved and restored.
/// </summary>
public sealed class Sept23Phase10ViewStateTests
{
    [AvaloniaFact]
    public async Task SecondLoad_PreservesALiveFilterChange()
    {
        var store = new CountingStore(new CatalogueViewState(
            "Grid", "Topic", null, null, null, null, null, null, "DateAdded", false, 1,
            QualityBelow: 0.5,
            MissingFields: "Isbn, Cover",
            NeedsOcrOnly: true,
            PossibleDuplicatesOnly: true));
        using var viewModel = new CatalogueViewModel(
            new ListReadModel(CreateBooks()),
            new NoOpNavigation(),
            new InMemoryLocalizationService(),
            viewStateStore: store);

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Topic", viewModel.Filter.TitleSearch);
        Assert.Equal(CatalogueSortField.DateAdded, viewModel.Filter.SortField);
        Assert.False(viewModel.Filter.SortAscending);
        Assert.Equal(0.5, viewModel.Filter.QualityBelow);
        Assert.Equal(CatalogueMissingFields.Isbn | CatalogueMissingFields.Cover, viewModel.Filter.MissingFields);
        Assert.True(viewModel.Filter.NeedsOcrOnly);
        Assert.True(viewModel.Filter.PossibleDuplicatesOnly);

        viewModel.Filter.ClearAll();
        viewModel.Filter.TitleSearch = "Topic 002";
        viewModel.Filter.SortField = CatalogueSortField.FileSize;
        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, store.Loads);
        Assert.Equal("Topic 002", viewModel.Filter.TitleSearch);
        Assert.Equal(CatalogueSortField.FileSize, viewModel.Filter.SortField);
        Assert.Equal(CatalogueMissingFields.None, viewModel.Filter.MissingFields);
        Assert.Equal(["book-002"], viewModel.FilteredItems.Select(book => book.BookId));
    }

    [AvaloniaFact]
    public async Task DamagedSavedCriteria_AreIgnored()
    {
        var store = new CountingStore(new CatalogueViewState(
            "Grid", null, null, null, null, null, null, null, "NoSuchSort", true, 1,
            QualityBelow: double.NaN,
            MissingFields: "Isbn, Bogus"));
        using var viewModel = new CatalogueViewModel(
            new ListReadModel(CreateBooks()),
            new NoOpNavigation(),
            new InMemoryLocalizationService(),
            viewStateStore: store);

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewModel.Filter.QualityBelow);
        Assert.Equal(CatalogueMissingFields.None, viewModel.Filter.MissingFields);
        Assert.Equal(CatalogueSortField.Title, viewModel.Filter.SortField);
        Assert.Equal(3, viewModel.FilteredItems.Count);
    }

    [Fact]
    public void OldViewStateFile_WithoutTheNewFields_StillLoads()
    {
        const string json = """
            {"View":"Grid","TitleSearch":null,"AuthorSearch":null,"StatusFilter":null,"MinRating":2,
             "MaxRating":null,"AvailabilityFilter":null,"SelectedShelfId":null,"SortField":"Year",
             "SortAscending":true,"CurrentPage":1}
            """;

        CatalogueViewState? state = JsonSerializer.Deserialize<CatalogueViewState>(json);

        Assert.NotNull(state);
        Assert.Equal(2, state.MinRating);
        Assert.True(state.ShowAll);
        Assert.Null(state.QualityBelow);
        Assert.Null(state.MissingFields);
        Assert.False(state.NeedsOcrOnly);
        Assert.False(state.PossibleDuplicatesOnly);
    }

    [Fact]
    public void SortLabels_AreLocalised()
    {
        var filter = new CatalogueFilterViewModel(new InMemoryLocalizationService()) { SortAscending = false };

        Assert.Equal("Descending", filter.SortDirectionText);
        Assert.Equal("Sort by date added", filter.SortFieldLabel(CatalogueSortField.DateAdded));
        Assert.All(
            Enum.GetValues<CatalogueSortField>(),
            field => Assert.StartsWith("Sort by", filter.SortFieldLabel(field), StringComparison.Ordinal));
    }

    private static List<BookSummaryProjection> CreateBooks() =>
        Enumerable.Range(1, 3)
            .Select(index => new BookSummaryProjection(
                $"book-{index:D3}",
                $"Topic {index:D3}",
                [$"Author {index:D3}"],
                null,
                0,
                null,
                [],
                null,
                true,
                2000 + index,
                FileSizeBytes: index * 100))
            .ToList();

    private sealed class CountingStore(CatalogueViewState state) : ICatalogueViewStateStore
    {
        public int Loads { get; private set; }

        public Task<CatalogueViewState?> LoadAsync(CancellationToken cancellationToken = default)
        {
            Loads++;
            return Task.FromResult<CatalogueViewState?>(state);
        }

        public Task SaveAsync(CatalogueViewState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ListReadModel(IReadOnlyList<BookSummaryProjection> books) : ICatalogueReadModel
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
