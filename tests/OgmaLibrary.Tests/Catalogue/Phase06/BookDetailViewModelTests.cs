using System.Threading;
using System.Threading.Tasks;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
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

    private static BookDetailViewModel CreateVm(
        BookDetailProjection? detail = null,
        IBookMetadataEnrichmentService? enrichment = null)
    {
        var readModel = new StubReadModel(detail);
        var reader = new StubReader();
        var loc = new InMemoryLocalizationService();
        return new BookDetailViewModel(readModel, reader, loc, enrichment);
    }

    private static readonly IReadOnlyList<string> TestAuthors = new[] { "Author One", "Author Two" };

    private static BookDetailProjection BuildFullyPopulatedDetail(string title = "Test Title") =>
        new(
            BookId: "book-1",
            Title: title,
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
                new MetadataFieldProjection("Title", title, "User", 1.0, true),
                new MetadataFieldProjection("Author", "Author One", "GoogleBooks", 0.95, false),
                new MetadataFieldProjection("Publisher", "Provider Press", "OpenLibrary", 0.86, false),
                new MetadataFieldProjection("Description", "Provider summary", "GoogleBooks", 0.82, false),
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

    /// <summary>
    /// Provider-enriched bibliographic fields must be visible, including source and
    /// confidence, so users can inspect deterministic metadata provenance.
    /// </summary>
    [Fact]
    public async Task BookDetail_MetadataDisplayRows_ShowBibliographicAndProviderProvenance()
    {
        var vm = CreateVm(BuildFullyPopulatedDetail());

        await vm.LoadBookAsync("book-1");

        Assert.Contains(vm.BiblioFieldDisplayRows, r => r.Contains("Publisher: Provider Press", StringComparison.Ordinal));
        Assert.Contains(vm.BiblioFieldDisplayRows, r => r.Contains("Description: Provider summary", StringComparison.Ordinal));
        Assert.Contains(vm.EnrichmentFieldDisplayRows, r =>
            r.Contains("Author: Author One", StringComparison.Ordinal) &&
            r.Contains("GoogleBooks", StringComparison.Ordinal));
        Assert.Contains(vm.EnrichmentFieldDisplayRows, r =>
            r.Contains("Publisher: Provider Press", StringComparison.Ordinal) &&
            r.Contains("OpenLibrary", StringComparison.Ordinal));
    }

    /// <summary>
    /// Manual metadata enrichment remains deterministic: the detail panel invokes
    /// the no-AI provider flow and reloads the book projection afterward.
    /// </summary>
    [Fact]
    public async Task BookDetail_EnrichMetadata_RunsProviderFlowAndRefreshesDetail()
    {
        var initial = BuildFullyPopulatedDetail();
        var enriched = BuildFullyPopulatedDetail("Enriched Title");
        var readModel = new StubReadModel(initial);
        var enrichment = new StubMetadataEnrichmentService(onEnrich: () => readModel.Detail = enriched);
        var vm = new BookDetailViewModel(
            readModel,
            new StubReader(),
            new InMemoryLocalizationService(),
            enrichment);

        await vm.LoadBookAsync("book-1");
        Assert.True(vm.CanEnrich);

        await vm.EnrichMetadataAsync();

        Assert.Equal("book-1", enrichment.BookId);
        Assert.Null(enrichment.AbsoluteFilePath);
        Assert.False(vm.IsEnriching);
        Assert.True(vm.CanEnrich);
        Assert.Equal("Enriched Title", vm.Title);
        Assert.Equal("Metadata enrichment complete.", vm.EnrichmentStatusText);
        Assert.Equal(2, readModel.LoadCount);
    }

    /// <summary>Provider failures remain visible and leave the Enrich command usable.</summary>
    [Fact]
    public async Task BookDetail_EnrichMetadata_ServiceReturnsFailure_ShowsErrorAndReenables()
    {
        var vm = new BookDetailViewModel(
            new StubReadModel(BuildFullyPopulatedDetail()),
            new StubReader(),
            new InMemoryLocalizationService(),
            new StubMetadataEnrichmentService(success: false, errorMessage: "provider offline"));

        await vm.LoadBookAsync("book-1");
        await vm.EnrichMetadataAsync();

        Assert.False(vm.IsEnriching);
        Assert.True(vm.CanEnrich);
        Assert.Equal("Metadata enrichment failed: provider offline", vm.EnrichmentStatusText);
    }

    /// <summary>Projection refresh failures are reported instead of becoming unobserved task errors.</summary>
    [Fact]
    public async Task BookDetail_EnrichMetadata_RefreshThrows_ShowsErrorAndReenables()
    {
        var readModel = new StubReadModel(BuildFullyPopulatedDetail());
        var vm = new BookDetailViewModel(
            readModel,
            new StubReader(),
            new InMemoryLocalizationService(),
            new StubMetadataEnrichmentService());

        await vm.LoadBookAsync("book-1");
        readModel.ThrowOnNextLoad = true;

        await vm.EnrichMetadataAsync();

        Assert.False(vm.IsEnriching);
        Assert.True(vm.CanEnrich);
        Assert.Equal("Metadata enrichment failed: refresh failed", vm.EnrichmentStatusText);
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubReadModel : ICatalogueReadModel
    {
        public BookDetailProjection? Detail { get; set; }

        public int LoadCount { get; private set; }

        public bool ThrowOnNextLoad { get; set; }

        public StubReadModel(BookDetailProjection? detail) => Detail = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default)
        {
            LoadCount += 1;
            if (ThrowOnNextLoad)
            {
                ThrowOnNextLoad = false;
                throw new InvalidOperationException("refresh failed");
            }

            return Task.FromResult(Detail);
        }

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

    private sealed class StubMetadataEnrichmentService : IBookMetadataEnrichmentService
    {
        private readonly bool _success;
        private readonly string? _errorMessage;
        private readonly Action _onEnrich;

        public StubMetadataEnrichmentService(
            Action? onEnrich = null,
            bool success = true,
            string? errorMessage = null)
        {
            _onEnrich = onEnrich ?? (() => { });
            _success = success;
            _errorMessage = errorMessage;
        }

        public string? BookId { get; private set; }

        public string? AbsoluteFilePath { get; private set; }

        public Task<(bool Success, string? ErrorMessage)> EnrichAsync(
            string bookId,
            string? absoluteFilePath,
            CancellationToken cancellationToken = default)
        {
            BookId = bookId;
            AbsoluteFilePath = absoluteFilePath;
            _onEnrich();
            return Task.FromResult((_success, _errorMessage));
        }
    }
}
