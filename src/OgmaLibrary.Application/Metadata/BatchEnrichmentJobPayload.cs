namespace OgmaLibrary.Application.Metadata;

/// <summary>Payload stored on per-book enrichment jobs created from a batch run.</summary>
public sealed record BatchEnrichmentJobPayload(
    string BatchId,
    int ChunkIndex,
    int ChunkSize,
    int OrdinalInChunk,
    string? FilePath = null);
