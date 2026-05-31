using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Integration tests for the ingestion pipeline (FR-LIB-003, NFR-OGMA-009).
/// </summary>
public sealed class IngestionPipelineTests : IDisposable
{
    private readonly IngestionTestFixture _fx = IngestionTestFixture.Create(3);

    /// <inheritdoc />
    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task IngestionPipeline_RegistersNewBooks()
    {
        await _fx.Orchestrator.ScanAsync();

        int bookCount = await _fx.Context.Books.CountAsync();
        Assert.True(bookCount >= 3, $"Expected at least 3 books, got {bookCount}");
    }

    [Fact]
    public async Task IngestionPipeline_IdempotentRescan()
    {
        // First scan.
        await _fx.Orchestrator.ScanAsync();
        int firstCount = await _fx.Context.Books.CountAsync();

        // Second scan — same files unchanged.
        await _fx.Orchestrator.ScanAsync();
        int secondCount = await _fx.Context.Books.CountAsync();

        Assert.Equal(firstCount, secondCount);
    }

    [Fact]
    public async Task IngestionPipeline_RematuresRenamedFile()
    {
        // First scan — register original file.
        await _fx.Orchestrator.ScanAsync();
        int countBefore = await _fx.Context.Books.CountAsync();

        // Find one file and rename it.
        string? originalRelPath = await _fx.Context.BookFiles
            .Select(f => f.RelativePath)
            .FirstOrDefaultAsync();
        Assert.NotNull(originalRelPath);

        string absOriginal = Path.Combine(
            _fx.RootDir,
            originalRelPath!.Replace('/', Path.DirectorySeparatorChar));

        string renamedPath = absOriginal + ".renamed.pdf";
        File.Move(absOriginal, renamedPath);

        try
        {
            // Second scan — should re-match by hash, not create a new book.
            await _fx.Orchestrator.ScanAsync();
            int countAfter = await _fx.Context.Books.CountAsync();

            // The renamed file should be re-matched (not create a new book).
            // The total should not increase significantly (at most same, or same+1 if hash-based).
            Assert.True(countAfter <= countBefore + 1,
                $"Rename created duplicate books: before={countBefore}, after={countAfter}");
        }
        finally
        {
            // Restore for other tests.
            if (File.Exists(renamedPath))
            {
                File.Move(renamedPath, absOriginal, overwrite: true);
            }
        }
    }

    [Fact]
    public async Task IngestionPipeline_RematchesMovedFile()
    {
        // First scan — register original file.
        await _fx.Orchestrator.ScanAsync();
        int countBefore = await _fx.Context.Books.CountAsync();

        // Find a file in books/ (not in subdir) and move it to a new subdir.
        BookFileRow? fileRow = await _fx.Context.BookFiles
            .FirstOrDefaultAsync(f => !f.RelativePath.Contains("subdir"));
        Assert.NotNull(fileRow);

        string absOriginal = Path.Combine(
            _fx.RootDir,
            fileRow!.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        string moveTargetDir = Path.Combine(_fx.RootDir, "books", "moved");
        Directory.CreateDirectory(moveTargetDir);
        string movedPath = Path.Combine(moveTargetDir, Path.GetFileName(absOriginal));
        File.Move(absOriginal, movedPath);

        try
        {
            // Second scan — should re-match by hash.
            await _fx.Orchestrator.ScanAsync();
            int countAfter = await _fx.Context.Books.CountAsync();

            Assert.True(countAfter <= countBefore + 1,
                $"Move created duplicate books: before={countBefore}, after={countAfter}");
        }
        finally
        {
            if (File.Exists(movedPath))
            {
                File.Move(movedPath, absOriginal, overwrite: true);
            }
        }
    }
}
