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

/// <summary>Deterministic dimensions for one generated visual-asset variant.</summary>
public sealed record VisualAssetVariantDefinition(
    string Name,
    VisualAssetKind Kind,
    int WidthPx,
    int HeightPx);

/// <summary>The bounded, on-demand visual-asset variants supported by the renderer.</summary>
public static class VisualAssetVariants
{
    /// <summary>The existing 200x300 catalogue cover.</summary>
    public static readonly VisualAssetVariantDefinition CoverDefault =
        new("default", VisualAssetKind.Cover, 200, 300);

    /// <summary>A 400x600 cover for detail and shelf focus states.</summary>
    public static readonly VisualAssetVariantDefinition CoverDetail =
        new("detail", VisualAssetKind.Cover, 400, 600);

    /// <summary>A 7x100 spine texture for the normal 3D shelf.</summary>
    public static readonly VisualAssetVariantDefinition SpineDefault =
        new("default", VisualAssetKind.Spine, 7, 100);

    /// <summary>A 14x200 spine texture for high-density displays.</summary>
    public static readonly VisualAssetVariantDefinition SpineRetina =
        new("retina", VisualAssetKind.Spine, 14, 200);

    /// <summary>Resolves a supported generated variant and rejects unbounded requests.</summary>
    public static VisualAssetVariantDefinition Resolve(VisualAssetKind kind, string variant) =>
        (kind, variant.Trim().ToLowerInvariant()) switch
        {
            (VisualAssetKind.Cover, "default") => CoverDefault,
            (VisualAssetKind.Cover, "detail") => CoverDetail,
            (VisualAssetKind.Spine, "default") => SpineDefault,
            (VisualAssetKind.Spine, "retina") => SpineRetina,
            _ => throw new ArgumentException(
                $"Unsupported generated {kind} variant '{variant}'.", nameof(variant)),
        };
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

    /// <summary>Returns one exact ready variant without falling back to another size.</summary>
    Task<VisualAssetDescriptor?> GetVariantAsync(
        string bookId,
        VisualAssetKind kind,
        string variant,
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

    /// <summary>Records a validated non-custom resolved asset such as provider art.</summary>
    Task<VisualAssetDescriptor> RegisterResolvedAsync(
        string bookId,
        string source,
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
