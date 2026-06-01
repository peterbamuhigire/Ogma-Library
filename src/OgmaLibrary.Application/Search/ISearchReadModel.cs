namespace OgmaLibrary.Application.Search;

/// <summary>
/// LAN-projection-ready read model for the Phase 10 search index lifecycle.
/// Phase 16 can subscribe to this stream without depending on the desktop UI
/// or on Index Manager command methods.
/// </summary>
public interface ISearchReadModel
{
    /// <summary>
    /// Emits durable search-index lifecycle events such as indexed books,
    /// failed books, and completed full rebuilds.
    /// </summary>
    IObservable<SearchIndexEvent> Events { get; }
}

/// <summary>
/// Search index lifecycle event projected by <see cref="ISearchReadModel"/>.
/// </summary>
public abstract record SearchIndexEvent
{
    /// <summary>A book has searchable chunks available.</summary>
    public sealed record BookIndexed(
        string BookId,
        int ChunkCount,
        DateTimeOffset IndexedAtUtc) : SearchIndexEvent;

    /// <summary>A book indexing attempt failed and needs user or worker attention.</summary>
    public sealed record BookIndexFailed(
        string BookId,
        string Reason,
        DateTimeOffset FailedAtUtc) : SearchIndexEvent;

    /// <summary>A full search-index rebuild completed.</summary>
    public sealed record IndexRebuilt(
        int TotalChunks,
        long DurationMs,
        DateTimeOffset RebuiltAtUtc) : SearchIndexEvent;
}
