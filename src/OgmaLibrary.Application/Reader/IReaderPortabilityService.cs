namespace OgmaLibrary.Application.Reader;

/// <summary>Versioned portable export/import boundary for local reader state.</summary>
public interface IReaderPortabilityService
{
    /// <summary>Exports progress, memory, bookmarks and annotations for one book.</summary>
    Task ExportAsync(
        string bookId,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>Imports a same-book export and returns inserted/updated counts.</summary>
    Task<ReaderImportResult> ImportAsync(
        string bookId,
        Stream source,
        CancellationToken cancellationToken = default);
}

/// <summary>Counts applied by one reader-state import.</summary>
public sealed record ReaderImportResult(
    bool ProgressApplied,
    bool ReadingMemoryApplied,
    int BookmarksApplied,
    int AnnotationsApplied);
