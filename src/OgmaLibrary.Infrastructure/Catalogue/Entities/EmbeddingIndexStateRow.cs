namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Durable singleton pointer for the active and staging vector indexes.</summary>
public sealed class EmbeddingIndexStateRow
{
    /// <summary>Stable singleton key.</summary>
    public string StateKey { get; set; } = "semantic";

    /// <summary>Index generation used by semantic retrieval.</summary>
    public string ActiveIndexVersion { get; set; } = "fts5-v1";

    /// <summary>Index generation currently being built, if any.</summary>
    public string? StagingIndexVersion { get; set; }

    /// <summary>UTC timestamp of the last pointer transition.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
