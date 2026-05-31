using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase06;

/// <summary>
/// Unit tests for FR-CAT-002: sort and filter behaviour, conjunction logic,
/// and clear action.
/// </summary>
public sealed class CatalogueFilterTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BookSummaryProjection MakeBook(
        string id,
        string title,
        string author,
        int? rating = null,
        int status = 0,
        bool isAvailable = true,
        int? year = null,
        IReadOnlyList<string>? shelfIds = null) =>
        new(
            BookId: id,
            Title: title,
            Authors: new[] { author },
            CoverRelativePath: null,
            Status: status,
            Rating: rating,
            ShelfIds: shelfIds ?? Array.Empty<string>(),
            ReadingProgressPct: null,
            IsAvailable: isAvailable,
            Year: year);

    private static CatalogueViewModel CreateVm(IEnumerable<BookSummaryProjection> books)
    {
        var readModel = new StubCatalogueReadModel(books);
        var nav = new StubNavigation();
        var loc = new OgmaLibrary.Infrastructure.Localization.InMemoryLocalizationService();
        return new CatalogueViewModel(readModel, nav, loc);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// FR-CAT-002: A single title filter returns only matching books.
    /// </summary>
    [Fact]
    public async Task FilterByTitle_ReturnsOnlyMatchingBooks()
    {
        var books = new[]
        {
            MakeBook("1", "Avalonia UI Guide", "A. Smith"),
            MakeBook("2", "Entity Framework Core", "B. Jones"),
            MakeBook("3", "Avalonia Advanced", "C. Lee"),
        };

        var vm = CreateVm(books);
        await vm.LoadAsync(CancellationToken.None);

        vm.Filter.TitleSearch = "Avalonia";

        Assert.Equal(2, vm.FilteredItems.Count);
        Assert.All(vm.FilteredItems, b => Assert.Contains("Avalonia", b.Title ?? "", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// FR-CAT-002: Multiple filter dimensions combine conjunctively.
    /// </summary>
    [Fact]
    public async Task FilterConjunctive_AllConditionsMustMatch()
    {
        var books = new[]
        {
            MakeBook("1", "Alpha Book", "Author A", rating: 5, status: 0),
            MakeBook("2", "Beta Book", "Author B", rating: 3, status: 0),
            MakeBook("3", "Gamma Book", "Author C", rating: 5, status: 1),
        };

        var vm = CreateVm(books);
        await vm.LoadAsync(CancellationToken.None);

        vm.Filter.MinRating = 5;
        vm.Filter.StatusFilter = 0;

        // Only book 1 has rating=5 AND status=0
        var single = Assert.Single(vm.FilteredItems);
        Assert.Equal("1", single.BookId);
    }

    /// <summary>
    /// FR-CAT-002: ClearAll resets all filter dimensions and HasActiveFilters returns false.
    /// </summary>
    [Fact]
    public async Task FilterClear_ResetsAllFilters()
    {
        var books = new[]
        {
            MakeBook("1", "Alpha Book", "Author A", rating: 5),
            MakeBook("2", "Beta Book", "Author B", rating: 2),
            MakeBook("3", "Gamma Book", "Author C", rating: 5),
        };

        var vm = CreateVm(books);
        await vm.LoadAsync(CancellationToken.None);

        // Apply some filters.
        vm.Filter.MinRating = 5;
        vm.Filter.TitleSearch = "Alpha";

        Assert.True(vm.Filter.HasActiveFilters);
        Assert.Single(vm.FilteredItems);

        // Clear all.
        vm.Filter.ClearAll();

        Assert.False(vm.Filter.HasActiveFilters);
        // After clear, all 3 books should be shown.
        Assert.Equal(3, vm.FilteredItems.Count);
    }

    /// <summary>
    /// FR-CAT-002: The displayed count equals the records satisfying the filter conjunction.
    /// </summary>
    [Fact]
    public async Task FilteredCount_EqualsMatchingRecords()
    {
        var books = Enumerable.Range(1, 10)
            .Select(i => MakeBook(
                i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"Book {i}",
                "Author",
                rating: i % 5 + 1,
                status: 0))
            .ToArray();

        var vm = CreateVm(books);
        await vm.LoadAsync(CancellationToken.None);

        vm.Filter.MinRating = 3;

        int expected = books.Count(b => b.Rating >= 3);
        Assert.Equal(expected, vm.FilteredCount);
    }

    /// <summary>
    /// Availability filter: available-only shows only available books.
    /// </summary>
    [Fact]
    public async Task FilterByAvailability_ShowsOnlyAvailableBooks()
    {
        var books = new[]
        {
            MakeBook("1", "Available Book", "A", isAvailable: true),
            MakeBook("2", "Missing Book", "B", isAvailable: false),
            MakeBook("3", "Another Available", "C", isAvailable: true),
        };

        var vm = CreateVm(books);
        await vm.LoadAsync(CancellationToken.None);

        vm.Filter.AvailabilityFilter = true;

        Assert.Equal(2, vm.FilteredItems.Count);
        Assert.All(vm.FilteredItems, b => Assert.True(b.IsAvailable));
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubCatalogueReadModel : ICatalogueReadModel
    {
        private readonly IEnumerable<BookSummaryProjection> _books;

        public StubCatalogueReadModel(IEnumerable<BookSummaryProjection> books)
        {
            _books = books;
        }

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (var book in _books)
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

    private sealed class StubNavigation :
        OgmaLibrary.Application.Navigation.IBookDetailNavigationService,
        OgmaLibrary.Application.Navigation.IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
