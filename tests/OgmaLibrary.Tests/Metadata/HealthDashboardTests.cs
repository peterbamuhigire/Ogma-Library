using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Tests for the library health dashboard (FR-META-007, power-librarian persona).
/// </summary>
public sealed class HealthDashboardTests
{
    [Fact]
    public async Task HealthDashboard_ShowsAllFiveCategories_WhenSeeded()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        // Duplicate ISBN books.
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_DUP1",
            Title = "Duplicate A",
            IsbnNormalized = "9780262033848",
            Status = 0,
        });
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_DUP2",
            Title = "Duplicate B",
            IsbnNormalized = "9780262033848",
            Status = 0,
        });

        // Book without ISBN.
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_NOISBN",
            Title = "No ISBN Book",
            Status = 0,
        });

        // Unavailable book.
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_UNAVAIL",
            Title = "Missing File",
            Status = 1, // Unavailable
            RelativePath = "missing.pdf",
        });

        // Failed job.
        context.Jobs.Add(new Infrastructure.Catalogue.Entities.JobRow
        {
            JobType = "Enrich",
            IdempotencyKey = "failed-key-001",
            Status = 3, // Failed
            BookId = "HD_NOISBN",
            ErrorMessage = "Provider timeout",
        });

        await context.SaveChangesAsync();

        var svc = new LibraryHealthService(context);
        var snapshot = await svc.GetHealthSnapshotAsync();

        // Duplicates section.
        Assert.NotEmpty(snapshot.Duplicates);
        Assert.Contains(snapshot.Duplicates, d => d.DuplicateKind == "ISBN");

        // Missing ISBNs (at least the no-ISBN book and the duplicate books without ISBN in their own field).
        Assert.NotEmpty(snapshot.MissingIsbns);
        Assert.Contains(snapshot.MissingIsbns, m => m.BookId == "HD_NOISBN");

        // Unavailable files.
        Assert.NotEmpty(snapshot.UnavailableFiles);
        Assert.Contains(snapshot.UnavailableFiles, u => u.BookId == "HD_UNAVAIL");

        // Failed jobs.
        Assert.NotEmpty(snapshot.FailedJobs);
        Assert.Contains(snapshot.FailedJobs, j => j.ErrorMessage == "Provider timeout");

        // Snapshot loaded time is recent.
        Assert.True(
            DateTimeOffset.UtcNow - snapshot.LoadedUtc < TimeSpan.FromSeconds(5),
            "LoadedUtc should be recent");
    }

    [Fact]
    public async Task HealthDashboard_MissingCovers_IncludesBooksWithoutCoverField()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_NOCOVER",
            Title = "No Cover Book",
            Status = 0,
        });
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_WITHCOVER",
            Title = "Has Cover Book",
            Status = 0,
        });
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "HD_WITHCOVER",
            FieldName = "Cover",
            Value = ".ogma/covers/ab/abc.jpg",
        });

        await context.SaveChangesAsync();

        var svc = new LibraryHealthService(context);
        var snapshot = await svc.GetHealthSnapshotAsync();

        Assert.Contains(snapshot.MissingCovers, m => m.BookId == "HD_NOCOVER");
        Assert.DoesNotContain(snapshot.MissingCovers, m => m.BookId == "HD_WITHCOVER");
    }

    [Fact]
    public async Task HealthDashboard_RetryJob_ResetsStatusToPending()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        context.Jobs.Add(new Infrastructure.Catalogue.Entities.JobRow
        {
            JobId = 1,
            JobType = "Enrich",
            IdempotencyKey = "retry-key-001",
            Status = 3, // Failed
            ErrorMessage = "Old error",
        });
        await context.SaveChangesAsync();

        var svc = new LibraryHealthService(context);
        await svc.RetryJobAsync(1);

        var job = context.Jobs.Single(j => j.JobId == 1);
        Assert.Equal(0, job.Status); // Pending
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public async Task HealthDashboard_DuplicateByContentHash_Detected()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        const string hash = "aabbccdd" + "00112233" + "aabbccdd" + "00112233" + "aabbccdd" + "00112233" + "aabbccdd" + "0011223344";
        string sha256 = hash[..64]; // 64 hex chars

        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_HASH1",
            Title = "Hash Dup A",
            Sha256Hash = sha256,
            Status = 0,
        });
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "HD_HASH2",
            Title = "Hash Dup B",
            Sha256Hash = sha256,
            Status = 0,
        });

        await context.SaveChangesAsync();

        var svc = new LibraryHealthService(context);
        var snapshot = await svc.GetHealthSnapshotAsync();

        Assert.Contains(snapshot.Duplicates, d => d.DuplicateKind == "ContentHash");
    }

    [Fact]
    public async Task BatchEnrichment_CreatesJobsForBooks()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "BE_BOOK1", Status = 0 });
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "BE_BOOK2", Status = 0 });
        await context.SaveChangesAsync();

        var orchestrator = new BatchEnrichmentOrchestrator(context);
        int created = await orchestrator.StartAsync(["BE_BOOK1", "BE_BOOK2"]);

        Assert.Equal(2, created);

        var jobs = context.Jobs.Where(j => j.JobType == "Enrich").ToList();
        Assert.Equal(2, jobs.Count);
    }

    [Fact]
    public async Task BatchEnrichment_Idempotent_DoesNotDuplicateJobs()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow { BookId = "BE_IDEM1", Status = 0 });
        await context.SaveChangesAsync();

        var orchestrator = new BatchEnrichmentOrchestrator(context);

        // First call.
        int first = await orchestrator.StartAsync(["BE_IDEM1"]);
        // Second call for same book.
        int second = await orchestrator.StartAsync(["BE_IDEM1"]);

        Assert.Equal(1, first);
        Assert.Equal(0, second); // Idempotent: no new job created.

        var jobs = context.Jobs.Where(j => j.JobType == "Enrich" && j.BookId == "BE_IDEM1").ToList();
        Assert.Single(jobs);
    }
}

