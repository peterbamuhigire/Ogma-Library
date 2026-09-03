namespace OgmaLibrary.Application.Search;

/// <summary>Extracts a bounded, page-aware table of contents from a PDF.</summary>
public interface ITocExtractionService
{
    /// <summary>Reads document outline entries without trusting their text or paths.</summary>
    Task<TocExtractionResult> ExtractAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
