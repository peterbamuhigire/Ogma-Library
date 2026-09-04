namespace OgmaLibrary.Application.Search;

/// <summary>
/// Durable active/staging pointer for the local semantic vector index.
/// </summary>
public interface IEmbeddingIndexLifecycleService
{
    /// <summary>Returns the current active index and any resumable staging index.</summary>
    Task<EmbeddingIndexState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts or resumes one staging generation. An existing staging generation
    /// is returned so a process restart resumes the same index.
    /// </summary>
    Task<EmbeddingIndexState> BeginRebuildAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken);

    /// <summary>Atomically makes a verified staging generation active.</summary>
    Task<EmbeddingIndexState> PromoteAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken);

    /// <summary>Abandons a failed staging generation without changing the active index.</summary>
    Task<EmbeddingIndexState> AbandonAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken);
}

/// <summary>Durable semantic-index pointer state.</summary>
public sealed record EmbeddingIndexState(
    string ActiveIndexVersion,
    string? StagingIndexVersion,
    DateTimeOffset UpdatedUtc);
