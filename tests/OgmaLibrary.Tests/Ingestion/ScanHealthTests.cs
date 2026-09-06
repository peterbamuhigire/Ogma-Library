using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for the scan health report (FR-LIB-007).
/// </summary>
public sealed class ScanHealthTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(2);

    /// <inheritdoc />
    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task HealthReport_ShowsFailedJobs()
    {
        // Seed a failed job.
        _fx.Context.Jobs.Add(new JobRow
        {
            JobType = "ThumbnailGeneration",
            IdempotencyKey = "health-test-failed-job",
            Status = 3, // Failed
            Payload = "C:\\Users\\student\\private\\file.pdf token=secret-value",
            ErrorMessage = "Render error for C:\\Users\\student\\private\\file.pdf",
            FailureCode = "thumbnail_render_failed",
            CompletedUtc = DateTimeOffset.UtcNow,
        });
        await _fx.Context.SaveChangesAsync();

        var report = await _fx.HealthService.GetReportAsync();

        ScanFailureItem failure = Assert.Single(
            report.FailedJobs,
            item => item.FailureCode == "thumbnail_render_failed");
        Assert.StartsWith("job:", failure.SourceReference, StringComparison.Ordinal);
        Assert.DoesNotContain("student", failure.SourceReference, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", failure.SourceReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthReport_ShowsMissingThumbnails()
    {
        // Seed a pending thumbnail job (represents a missing thumbnail).
        _fx.Context.Jobs.Add(new JobRow
        {
            JobType = "ThumbnailGeneration",
            IdempotencyKey = "health-test-missing-thumb",
            Status = 0, // Pending — thumbnail not generated yet
            Payload = "test/file.pdf",
        });
        await _fx.Context.SaveChangesAsync();

        var report = await _fx.HealthService.GetReportAsync();

        Assert.NotEmpty(report.MissingThumbnails);
    }

    [Fact]
    public async Task HealthReport_ShowsMetadataGaps()
    {
        // Register a book without metadata fields.
        await _fx.Orchestrator.ScanAsync();

        // Books that were registered won't have Title metadata (QuestPDF doesn't set DocInfo).
        var report = await _fx.HealthService.GetReportAsync();

        // All registered books should show up as metadata gaps (no Title from QuestPDF).
        int bookCount = await _fx.Context.Books.CountAsync();
        if (bookCount > 0)
        {
            Assert.NotEmpty(report.MetadataGaps);
        }
    }

    [Fact]
    public async Task HealthReport_RetryAll_RequeuesFailedJobs()
    {
        // Seed 3 failed jobs.
        for (int i = 0; i < 3; i++)
        {
            _fx.Context.Jobs.Add(new JobRow
            {
                JobType = "ThumbnailGeneration",
                IdempotencyKey = $"retry-all-test-{i}",
                Status = 3, // Failed
                Payload = $"test/file-{i}.pdf",
                ErrorMessage = "Test error",
                CompletedUtc = DateTimeOffset.UtcNow,
            });
        }

        await _fx.Context.SaveChangesAsync();

        // Retry all failed jobs.
        await _fx.HealthService.RetryAllFailedAsync();

        // All should now be Pending.
        var jobs = await _fx.Context.Jobs
            .Where(j => j.IdempotencyKey.StartsWith("retry-all-test-"))
            .ToListAsync();

        Assert.Equal(3, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(0, j.Status)); // Pending
    }
}
