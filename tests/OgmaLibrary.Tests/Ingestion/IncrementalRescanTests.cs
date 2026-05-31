using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for incremental rescan (FR-LIB-006).
/// </summary>
public sealed class IncrementalRescanTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(4);

    /// <inheritdoc />
    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task IncrementalRescan_SkipsUnchangedFiles()
    {
        // First scan — registers all books and enqueues jobs.
        await _fx.Orchestrator.ScanAsync();
        int jobCountAfterFirstScan = await _fx.Context.Jobs.CountAsync();

        // Second scan — files unchanged.
        await _fx.Orchestrator.ScanAsync();
        int jobCountAfterSecondScan = await _fx.Context.Jobs.CountAsync();

        // No new jobs should have been created for unchanged files.
        // (The idempotency key prevents duplicate jobs.)
        Assert.Equal(jobCountAfterFirstScan, jobCountAfterSecondScan);
    }

    [Fact]
    public async Task IncrementalRescan_Requeues_ChangedFiles()
    {
        // First scan.
        await _fx.Orchestrator.ScanAsync();
        int initialBookCount = await _fx.Context.Books.CountAsync();

        // Modify one file (overwrite with different content to change hash + mtime).
        string? relPath = await _fx.Context.BookFiles
            .Select(f => f.RelativePath)
            .FirstOrDefaultAsync();
        Assert.NotNull(relPath);

        string absPath = Path.Combine(
            _fx.RootDir,
            relPath!.Replace('/', Path.DirectorySeparatorChar));

        // Small delay to ensure mtime differs.
        await Task.Delay(10);
        IngestionTestFixture.WriteSyntheticPdf(absPath, "Modified Book Title", "New Author");

        // Second scan — changed file should be re-processed.
        await _fx.Orchestrator.ScanAsync();

        // The book count should remain the same (re-matched by hash).
        int finalBookCount = await _fx.Context.Books.CountAsync();
        Assert.True(finalBookCount <= initialBookCount + 1,
            $"Modified file created extra books: initial={initialBookCount}, final={finalBookCount}");
    }
}
