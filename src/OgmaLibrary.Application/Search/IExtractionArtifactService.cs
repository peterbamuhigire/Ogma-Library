namespace OgmaLibrary.Application.Search;

/// <summary>Lifecycle state of one versioned extraction artifact run.</summary>
public enum ExtractionArtifactStatus
{
    /// <summary>Extraction has begun but its manifest is not complete.</summary>
    Pending = 0,

    /// <summary>Extraction output passed the pipeline’s completion contract.</summary>
    Completed = 1,

    /// <summary>Extraction failed and must be reprocessed.</summary>
    Failed = 2,
}

/// <summary>Durable extraction-artifact projection.</summary>
public sealed record ExtractionArtifactDescriptor(
    long Id,
    string BookId,
    string? ContentHash,
    string ExtractorVersion,
    ExtractionArtifactStatus Status,
    int PagesProcessed,
    int FailedPages,
    string? ManifestHash,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? CompletedUtc);

/// <summary>Records versioned extraction output independent of the search index.</summary>
public interface IExtractionArtifactService
{
    /// <summary>Starts or returns the existing run for a book/content/version tuple.</summary>
    Task<ExtractionArtifactDescriptor> BeginAsync(
        string bookId,
        string? contentHash,
        string extractorVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Completes a pending artifact with validated counts and manifest hash.</summary>
    Task<ExtractionArtifactDescriptor> CompleteAsync(
        long artifactId,
        int pagesProcessed,
        int failedPages,
        string manifestHash,
        CancellationToken cancellationToken = default);

    /// <summary>Marks a run failed without persisting extracted document content.</summary>
    Task<ExtractionArtifactDescriptor> FailAsync(
        long artifactId,
        CancellationToken cancellationToken = default);
}
