namespace OgmaLibrary.Application.Catalogue;

/// <summary>Visual asset families shared by the catalogue and 3D shelf.</summary>
public enum VisualAssetKind
{
    /// <summary>Primary book cover.</summary>
    Cover = 0,

    /// <summary>Small catalogue or page thumbnail.</summary>
    Thumbnail = 1,

    /// <summary>Book-spine texture.</summary>
    Spine = 2,
}

/// <summary>Lifecycle state of a visual asset manifest entry.</summary>
public enum VisualAssetStatus
{
    /// <summary>Generation has been requested but is not ready.</summary>
    Pending = 0,

    /// <summary>The manifest entry points to a validated asset.</summary>
    Ready = 1,

    /// <summary>Generation failed; the entry is retained for diagnostics.</summary>
    Failed = 2,

    /// <summary>The source content changed and the generated asset must be replaced.</summary>
    Stale = 3,
}

/// <summary>LAN-safe description of a resolved visual asset.</summary>
public sealed record VisualAssetDescriptor(
    string BookId,
    VisualAssetKind Kind,
    string Variant,
    string RelativePath,
    string Source,
    string? SourceContentHash,
    int WidthPx,
    int HeightPx,
    string Format,
    int GenerationVersion,
    VisualAssetStatus Status,
    bool IsCustom,
    DateTimeOffset UpdatedUtc);

/// <summary>Result of one stale visual-asset garbage-collection pass.</summary>
public sealed record VisualAssetGarbageCollectionResult(
    int RemovedManifestEntries,
    int DeletedFiles,
    int RetainedReferencedFiles);

/// <summary>Durable manifest and precedence boundary for visual assets.</summary>
public interface IVisualAssetService
{
    /// <summary>Returns the highest-precedence ready variant for a book and kind.</summary>
    Task<VisualAssetDescriptor?> GetPreferredAsync(
        string bookId,
        VisualAssetKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a deterministic generated asset. A custom cover is never replaced by
    /// this operation, even when the source PDF hash changes.
    /// </summary>
    Task<VisualAssetDescriptor> RegisterGeneratedAsync(
        string bookId,
        string? sourceContentHash,
        VisualAssetKind kind,
        string variant,
        string relativePath,
        int widthPx,
        int heightPx,
        string format,
        int generationVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Registers a user-selected cover and locks it against regeneration.</summary>
    Task<VisualAssetDescriptor> RegisterCustomCoverAsync(
        string bookId,
        string relativePath,
        int widthPx,
        int heightPx,
        string format,
        CancellationToken cancellationToken = default);

    /// <summary>Marks generated assets from an old source hash stale.</summary>
    Task<int> InvalidateGeneratedAsync(
        string bookId,
        string? currentSourceContentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes stale manifest entries and unreferenced sidecar files. A null book
    /// ID collects stale generated assets across the library.
    /// </summary>
    Task<VisualAssetGarbageCollectionResult> CollectStaleAsync(
        string? bookId = null,
        CancellationToken cancellationToken = default);
}
