using Microsoft.EntityFrameworkCore;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 Phase 10 (task 10.10, META-008): the library health dashboard. Tile counts equal
/// direct database counts on a seeded catalogue, each tile applies the matching catalogue
/// filter, failed jobs can be retried, and batch controls follow the provider capability.
/// </summary>
public sealed class Sept23Phase10LibraryHealthTests
{
    [Fact]
    public async Task Tiles_CountWhatTheDatabaseHolds()
    {
        using CatalogueDbContext context = await SeedWithFailedJobAsync();
        var readModel = new CatalogueReadModel(context);
        LibraryHealthViewModel health = Create(readModel, new LibraryHealthService(context), new CatalogueFilterViewModel());

        await health.LoadAsync();

        // Direct database counts: the seed's rows, read without the read model.
        List<BookRow> books = await context.Books.AsNoTracking().ToListAsync();
        int failedJobs = await context.Jobs.CountAsync(job => job.Status == 3);
        Assert.Equal(books.Count(book => book.Status == 1), Count(health, LibraryHealthTileKind.Unavailable));
        Assert.Equal(failedJobs, Count(health, LibraryHealthTileKind.FailedJobs));
        Assert.Equal(1, failedJobs);

        // The seed is the criteria seed whose rows were verified per predicate.
        Assert.Equal(3, Count(health, LibraryHealthTileKind.MissingAuthor));
        Assert.Equal(4, Count(health, LibraryHealthTileKind.MissingIsbn));
        Assert.Equal(6, Count(health, LibraryHealthTileKind.MissingCover));
        Assert.Equal(2, Count(health, LibraryHealthTileKind.MissingYear));
        Assert.Equal(1, Count(health, LibraryHealthTileKind.PossibleDuplicates));

        // Each filter tile equals the read model's count for the filter the tile applies.
        foreach (LibraryHealthTileViewModel tile in health.Tiles.Where(tile => tile.AppliesFilter))
        {
            var filter = new CatalogueFilterViewModel();
            LibraryHealthViewModel.ApplyFilter(tile.Kind, filter);
            int expected = await ((ICatalogueReadModel)readModel).CountAsync(filter.ToCriteria());
            Assert.Equal(expected, tile.Count);
        }

        Assert.Single(health.FailedJobs);
        Assert.All(health.Tiles, tile => Assert.DoesNotContain("⟦", tile.AccessibleName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(LibraryHealthTileKind.MissingAuthor)]
    [InlineData(LibraryHealthTileKind.MissingIsbn)]
    [InlineData(LibraryHealthTileKind.MissingCover)]
    [InlineData(LibraryHealthTileKind.MissingYear)]
    [InlineData(LibraryHealthTileKind.Unavailable)]
    [InlineData(LibraryHealthTileKind.PossibleDuplicates)]
    [InlineData(LibraryHealthTileKind.LowQuality)]
    public async Task TileClick_ReplacesTheFilterAndShowsTheCatalogue(LibraryHealthTileKind kind)
    {
        using CatalogueDbContext context = await SeedWithFailedJobAsync();
        var filter = new CatalogueFilterViewModel { TitleSearch = "stale", MinRating = 5 };
        LibraryHealthViewModel health = Create(new CatalogueReadModel(context), new LibraryHealthService(context), filter);
        int shown = 0;
        health.ShowCatalogueRequested += (_, _) => shown++;

        health.ActivateTile(kind);

        Assert.Equal(1, shown);
        Assert.Null(filter.TitleSearch);
        Assert.Null(filter.MinRating);
        CatalogueFilter criteria = filter.ToCriteria();
        CatalogueFilter expected = kind switch
        {
            LibraryHealthTileKind.MissingAuthor => new CatalogueFilter(MissingFields: CatalogueMissingFields.Author),
            LibraryHealthTileKind.MissingIsbn => new CatalogueFilter(MissingFields: CatalogueMissingFields.Isbn),
            LibraryHealthTileKind.MissingCover => new CatalogueFilter(MissingFields: CatalogueMissingFields.Cover),
            LibraryHealthTileKind.MissingYear => new CatalogueFilter(MissingFields: CatalogueMissingFields.Year),
            LibraryHealthTileKind.Unavailable => new CatalogueFilter(IsAvailable: false),
            LibraryHealthTileKind.PossibleDuplicates => new CatalogueFilter(PossibleDuplicates: true),
            _ => new CatalogueFilter(QualityBelow: LibraryHealthViewModel.LowQualityThreshold),
        };
        Assert.Equal(expected with { SortField = criteria.SortField, SortAscending = criteria.SortAscending }, criteria);
    }

    [Fact]
    public async Task FailedJobsTile_OpensTheJobList_AndRetryRequeuesTheJob()
    {
        using CatalogueDbContext context = await SeedWithFailedJobAsync();
        var service = new LibraryHealthService(context);
        LibraryHealthViewModel health = Create(new CatalogueReadModel(context), service, new CatalogueFilterViewModel());
        int shown = 0;
        health.ShowCatalogueRequested += (_, _) => shown++;
        await health.LoadAsync();

        health.ActivateTile(LibraryHealthTileKind.FailedJobs);
        Assert.True(health.IsFailedJobsExpanded);
        Assert.Equal(0, shown);

        await health.RetryJobAsync(health.FailedJobs[0].JobId);

        Assert.Empty(health.FailedJobs);
        Assert.Equal(0, Count(health, LibraryHealthTileKind.FailedJobs));
        Assert.Equal(0, await context.Jobs.CountAsync(job => job.Status == 3));
    }

    [Fact]
    public async Task NeedsAttentionTile_AsksTheShellForTheNeedsAttentionList()
    {
        using CatalogueDbContext context = await SeedWithFailedJobAsync();
        LibraryHealthViewModel health = Create(new CatalogueReadModel(context), new LibraryHealthService(context), new CatalogueFilterViewModel());
        int asked = 0;
        health.ShowNeedsAttentionRequested += (_, _) => asked++;

        health.ActivateTile(LibraryHealthTileKind.NeedsAttention);

        Assert.Equal(1, asked);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task BatchControls_ShowOnlyWhenProvidersAreEnabled(bool providersEnabled, bool visible)
    {
        var service = new FakeHealthService();
        var capabilities = new FakeCapabilities { AreMetadataProvidersEnabled = providersEnabled };
        var health = new LibraryHealthViewModel(
            new EmptyReadModel(), service, new CatalogueFilterViewModel(), new InMemoryLocalizationService(), capabilities);

        await health.LoadAsync();

        Assert.Single(health.BatchRuns);
        Assert.Equal(visible, health.IsBatchControlsVisible);
        if (visible)
        {
            await health.PauseBatchAsync("batch-1");
            await health.ResumeBatchAsync("batch-1");
            Assert.Equal(["pause:batch-1", "resume:batch-1"], service.Calls);
        }
    }

    private static int Count(LibraryHealthViewModel health, LibraryHealthTileKind kind) =>
        health.Tiles.Single(tile => tile.Kind == kind).Count;

    private static LibraryHealthViewModel Create(
        ICatalogueReadModel readModel,
        ILibraryHealthService service,
        CatalogueFilterViewModel filter) =>
        new(readModel, service, filter, new InMemoryLocalizationService());

    private static async Task<CatalogueDbContext> SeedWithFailedJobAsync()
    {
        CatalogueDbContext context = await Sept23Phase10CriteriaTests.SeedAsync();
        context.Jobs.Add(new JobRow
        {
            JobType = "Enrich",
            IdempotencyKey = "p10-health-failed",
            Status = 3,
            BookId = "P10-B",
            ErrorMessage = "provider_timeout",
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FakeHealthService : ILibraryHealthService
    {
        public List<string> Calls { get; } = [];

        public Task<LibraryHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryHealthSnapshot(
                [], [], [], [], [], DateTimeOffset.UtcNow,
                [new BatchEnrichmentRunEntry("batch-1", 4, 2, 0, 1, 0, 1)]));

        public Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default)
        {
            Calls.Add("pause:" + batchId);
            return Task.CompletedTask;
        }

        public Task ResumeBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default)
        {
            Calls.Add("resume:" + batchId);
            return Task.CompletedTask;
        }

        public Task<string> ExportFailedJobsCsvAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);
    }

    private sealed class EmptyReadModel : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
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

    private sealed class FakeCapabilities : OgmaLibrary.Application.Navigation.ICapabilityState
    {
        public event EventHandler? Changed { add { } remove { } }

        public bool AreMetadataProvidersEnabled { get; init; }

        public bool IsAiConfigured => false;

        public bool IsClassroomHostEnabled => false;

        public bool IsClassroomClientConnected => false;

        public bool IsClassroomAvailable => false;

        public bool IsShelf3DAvailable => false;
    }
}
