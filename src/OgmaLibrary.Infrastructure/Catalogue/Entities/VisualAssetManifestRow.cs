namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Durable manifest row for one book visual asset variant.</summary>
public sealed class VisualAssetManifestRow
{
    /// <summary>Owning legacy/canonical-compatible book identity.</summary>
    public string BookId { get; set; } = string.Empty;

    /// <summary>Visual asset family (see Application VisualAssetKind).</summary>
    public int Kind { get; set; }

    /// <summary>Stable variant name, such as default, low, or high.</summary>
    public string Variant { get; set; } = string.Empty;

    /// <summary>Portable sidecar-relative URI/path.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Non-secret source label, such as generated, provider, or custom.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Source PDF/content hash used for generated invalidation.</summary>
    public string? SourceContentHash { get; set; }

    /// <summary>Pixel width.</summary>
    public int WidthPx { get; set; }

    /// <summary>Pixel height.</summary>
    public int HeightPx { get; set; }

    /// <summary>Encoded format, for example jpeg or png.</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Generator contract version.</summary>
    public int GenerationVersion { get; set; }

    /// <summary>Manifest lifecycle status.</summary>
    public int Status { get; set; }

    /// <summary>Whether this row is user-selected and protected from generation.</summary>
    public bool IsCustom { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Last manifest update timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Owning book.</summary>
    public BookRow? Book { get; set; }
}
