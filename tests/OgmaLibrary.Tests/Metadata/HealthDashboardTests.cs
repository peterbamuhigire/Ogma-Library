using System.Text.Json;
using OgmaLibrary.Application.Ingestion;
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
        Assert.Contains(snapshot.FailedJobs, job => job.FailureCode == "job_failed");

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
            FailureCode = "old_failure",
            CompletedUtc = DateTimeOffset.UtcNow,
            LeaseOwner = "expired-worker",
            LeaseExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            NextAttemptUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        await context.SaveChangesAsync();

        var svc = new LibraryHealthService(context);
        await svc.RetryJobAsync(1);

        var job = context.Jobs.Single(j => j.JobId == 1);
        Assert.Equal(0, job.Status); // Pending
        Assert.Null(job.ErrorMessage);
        Assert.Null(job.FailureCode);
        Assert.Null(job.CompletedUtc);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseExpiresUtc);
        Assert.True(job.NextAttemptUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HealthDashboard_RetryJob_IgnoresCompletedJob()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        DateTimeOffset completedUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        context.Jobs.Add(new Infrastructure.Catalogue.Entities.JobRow
        {
            JobId = 1,
            JobType = "Enrich",
            IdempotencyKey = "completed-key-001",
            Status = 2, // Completed
            CompletedUtc = completedUtc,
        });
        await context.SaveChangesAsync();

        var svc = new LibraryHealthService(context);
        await svc.RetryJobAsync(1);

        var job = context.Jobs.Single(j => j.JobId == 1);
        Assert.Equal(2, job.Status);
        Assert.Equal(completedUtc, job.CompletedUtc);
        Assert.Equal(0, job.RetryCount);
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

    [Fact]
    public async Task BatchEnrichment_ChunksJobsInRecoverableGroupsOf50()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        string[] bookIds = Enumerable.Range(1, 120)
            .Select(i => $"BE_SCALE_{i:0000}")
            .ToArray();
        foreach (string bookId in bookIds)
        {
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = bookId,
                Status = 0,
            });
        }

        await context.SaveChangesAsync();

        var orchestrator = new BatchEnrichmentOrchestrator(context);
        int created = await orchestrator.StartAsync(bookIds);

        Assert.Equal(120, created);

        var payloads = context.Jobs
            .Where(j => j.JobType == "Enrich")
            .OrderBy(j => j.BookId)
            .Select(j => JsonSerializer.Deserialize<BatchEnrichmentJobPayload>(j.Payload!))
            .ToList();

        Assert.All(payloads, Assert.NotNull);
        Assert.Equal(3, payloads.Select(p => p!.ChunkIndex).Distinct().Count());
        Assert.Equal(50, payloads.Count(p => p!.ChunkIndex == 0));
        Assert.Equal(50, payloads.Count(p => p!.ChunkIndex == 1));
        Assert.Equal(20, payloads.Count(p => p!.ChunkIndex == 2));
        Assert.All(payloads.Where(p => p!.ChunkIndex == 2), p => Assert.Equal(20, p!.ChunkSize));
    }

    [Fact]
    public async Task HealthDashboard_BatchEnrichmentPauseResumeAndFailedCsv_AreOperatorVisible()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();
        const string batchId = "batch-operator";
        SeedBatchJob(context, batchId, "BE_OP_1", status: 0);
        SeedBatchJob(context, batchId, "BE_OP_2", status: 1);
        SeedBatchJob(
            context,
            batchId,
            "BE_OP_3",
            status: 3,
            error: "Provider timeout at C:\\Users\\student\\library.pdf token=secret-value");
        context.Jobs.Local.Single(job => job.BookId == "BE_OP_3").FailureCode = "provider_timeout";
        context.Jobs.Local.Single(job => job.BookId == "BE_OP_3").NextAttemptUtc = DateTimeOffset.UtcNow.AddHours(1);
        await context.SaveChangesAsync();

        var service = new LibraryHealthService(context);

        LibraryHealthSnapshot initial = await service.GetHealthSnapshotAsync();
        BatchEnrichmentRunEntry run = Assert.Single(initial.BatchEnrichmentRuns!);
        Assert.Equal(3, run.TotalJobs);
        Assert.Equal(1, run.PendingJobs);
        Assert.Equal(1, run.RunningJobs);
        Assert.Equal(1, run.FailedJobs);

        string csv = await service.ExportFailedJobsCsvAsync();
        Assert.Contains("JobId,JobType,BookId,FailureCode,FailedUtc", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("student", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", csv, StringComparison.Ordinal);
        Assert.Contains("provider_timeout", csv, StringComparison.Ordinal);

        await service.PauseBatchEnrichmentAsync(batchId);
        Assert.Equal(1, context.Jobs.Count(job => job.Status == 6));
        Assert.Contains(context.Jobs, job => job.BookId == "BE_OP_2" && job.Status == 1);

        await service.ResumeBatchEnrichmentAsync(batchId);
        Assert.Equal(2, context.Jobs.Count(job => job.Status == 0));
        Assert.Contains(context.Jobs, job => job.BookId == "BE_OP_2" && job.Status == 1);
        Assert.DoesNotContain(context.Jobs, job => job.ErrorMessage != null);
        Assert.DoesNotContain(context.Jobs, job => job.FailureCode != null);
        Assert.All(
            context.Jobs.Where(job => job.Status == (int)JobRuntimeStatus.Pending),
            job => Assert.True(job.NextAttemptUtc <= DateTimeOffset.UtcNow));
        Assert.Contains(context.Jobs, job => job.BookId == "BE_OP_3" && job.RetryCount == 1);
    }

    [Fact]
    public async Task BatchEnrichment_2000Books_CompletesWithRetry()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        string[] bookIds = Enumerable.Range(1, 2_000)
            .Select(i => $"BE_2000_{i:0000}")
            .ToArray();
        foreach (string bookId in bookIds)
        {
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = bookId,
                Status = 0,
            });
        }

        await context.SaveChangesAsync();
        var orchestrator = new BatchEnrichmentOrchestrator(context);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        int created = await orchestrator.StartAsync(bookIds);
        string batchId = context.Jobs
            .Where(job => job.JobType == "Enrich")
            .Select(job => JsonSerializer.Deserialize<BatchEnrichmentJobPayload>(job.Payload!)!.BatchId)
            .First();
        int ordinal = 0;
        foreach (var job in context.Jobs.Where(job => job.JobType == "Enrich").OrderBy(job => job.JobId))
        {
            ordinal++;
            job.Status = ordinal % 5 == 0 ? 3 : 2;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.ErrorMessage = job.Status == 3 ? "Synthetic 429 retry" : null;
        }

        await context.SaveChangesAsync();
        var service = new LibraryHealthService(context);
        await service.ResumeBatchEnrichmentAsync(batchId);
        foreach (var job in context.Jobs.Where(job => job.JobType == "Enrich" && job.Status == 0))
        {
            job.Status = 2;
            job.CompletedUtc = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();
        stopwatch.Stop();

        Assert.Equal(2_000, created);
        int chunkCount = context.Jobs
            .Where(job => job.JobType == "Enrich")
            .AsEnumerable()
            .Select(job => JsonSerializer.Deserialize<BatchEnrichmentJobPayload>(job.Payload!)!.ChunkIndex)
            .Distinct()
            .Count();
        Assert.Equal(40, chunkCount);
        Assert.Equal(2_000, context.Jobs.Count(job => job.JobType == "Enrich" && job.Status == 2));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"2,000-book batch simulation took {stopwatch.Elapsed}.");
    }

    private static void SeedBatchJob(
        Infrastructure.Catalogue.CatalogueDbContext context,
        string batchId,
        string bookId,
        int status,
        string? error = null)
    {
        context.Jobs.Add(new Infrastructure.Catalogue.Entities.JobRow
        {
            JobType = "Enrich",
            BookId = bookId,
            IdempotencyKey = $"{batchId}-{bookId}",
            Status = status,
            Payload = JsonSerializer.Serialize(new BatchEnrichmentJobPayload(
                batchId,
                ChunkIndex: 0,
                ChunkSize: 3,
                OrdinalInChunk: 0)),
            ErrorMessage = error,
            CompletedUtc = status == 3 ? DateTimeOffset.UtcNow : null,
        });
    }
}

