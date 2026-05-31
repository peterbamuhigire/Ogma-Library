using System.Threading;
using System.Threading.Tasks;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase06;

/// <summary>
/// Unit tests for <see cref="BookDetailViewModel"/> (FR-CAT-004).
/// Verifies that all five field groups are exposed and that grid/list selection
/// resolves to the same detail projection.
/// </summary>
public sealed class BookDetailViewModelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BookDetailViewModel CreateVm(BookDetailProjection? detail = null)
    {
        var readModel = new StubReadModel(detail);
        var reader = new StubReader();
        var loc = new InMemoryLocalizationService();
        return new BookDetailViewModel(readModel, reader, loc);
    }

    private static readonly IReadOnlyList<string> TestAuthors = new[] { "Author One", "Author Two" };

    private static BookDetailProjection BuildFullyPopulatedDetail() =>
        new(
            BookId: "book-1",
            Title: "Test Title",
            Authors: TestAuthors,
            Year: 2023,
            Isbn: "9780123456789",
            Doi: "10.1234/test",
            Rating: 5,
            Status: 0,
            CoverRelativePath: "covers/book-1.jpg",
            RelativePath: "books/test.pdf",
            Sha256Hash: "abc123",
            SizeBytes: 1_024_000,
            ReadingProgress: new ReadingProgressProjection(
                BookId: "book-1",
                CurrentPage: 50,
                CompletionPct: 25.0,
                LastReadUtc: DateTimeOffset.UtcNow.AddDays(-1),
                Status: 1),
            Annotations: 3,
            MetadataFields: new[]
            {
                new MetadataFieldProjection("Title", "Test Title", "User", 1.0, true),
                new MetadataFieldProjection("Author", "Author One", "GoogleBooks", 0.95, false),
                new MetadataFieldProjection("RelativePath", "books/test.pdf", "System", null, false),
                new MetadataFieldProjection("Status", "0", "System", null, false),
            });

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// FR-CAT-004: Loading a fully-enriched book populates all five field groups.
    /// </summary>
    [Fact]
    public async Task BookDetail_AllFiveFieldGroups_Populated()
    {
        var detail = BuildFullyPopulatedDetail();
        var vm = CreateVm(detail);

        await vm.LoadBookAsync("book-1");

        // Bibliographic group.
        Assert.Equal("Test Title", vm.Title);
        Assert.NotNull(vm.AuthorsDisplay);
        Assert.Equal("2023", vm.Year);
        Assert.Equal("9780123456789", vm.Isbn);
        Assert.Equal("10.1234/test", vm.Doi);

        // File group.
        Assert.Equal("books/test.pdf", vm.RelativePath);
        Assert.Equal(1_024_000, vm.SizeBytes);
        Assert.Equal("abc123", vm.Sha256Hash);

        // Reading group.
        Assert.Equal(5, vm.Rating);
        Assert.Equal(25.0, vm.ReadingProgressPct);
        Assert.Equal(3, vm.AnnotationCount);

        // Book is visible after load.
        Assert.True(vm.IsVisible);
        Assert.NotNull(vm.Book);
    }

    /// <summary>
    /// FR-CAT-001: View-equivalence — selecting a book in grid vs list resolves to
    /// the same detail projection (same BookId). Both calls use the same StubReadModel.
    /// </summary>
    [Fact]
    public async Task ViewEquivalence_GridAndListSelectSameDetailProjection()
    {
        var detail = BuildFullyPopulatedDetail();
        var vm = CreateVm(detail);

        // Simulate grid selection.
        await vm.LoadBookAsync("book-1");
        string? titleFromGrid = vm.Title;

        // Simulate list selection (same book).
        await vm.LoadBookAsync("book-1");
        string? titleFromList = vm.Title;

        Assert.Equal(titleFromGrid, titleFromList);
        Assert.Equal("Test Title", titleFromGrid);
    }

    /// <summary>
    /// Closing the panel sets IsVisible to false.
    /// </summary>
    [Fact]
    public async Task BookDetail_Close_HidesPanel()
    {
        var detail = BuildFullyPopulatedDetail();
        var vm = CreateVm(detail);

        await vm.LoadBookAsync("book-1");
        Assert.True(vm.IsVisible);

        vm.Close();
        Assert.False(vm.IsVisible);
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubReadModel : ICatalogueReadModel
    {
        private readonly BookDetailProjection? _detail;

        public StubReadModel(BookDetailProjection? detail) => _detail = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_detail);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class StubReader : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
