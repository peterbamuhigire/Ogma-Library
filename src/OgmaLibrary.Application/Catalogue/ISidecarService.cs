namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Classes of sidecar assets produced or consumed by Ogma Library
/// (ADR-0005, Phase 04 WP5). Each class maps to a sub-folder under the sidecar root.
/// </summary>
public enum SidecarClass
{
    /// <summary>Cover images (200×300 JPEG thumbnails).</summary>
    Covers = 0,

    /// <summary>Per-page thumbnail images.</summary>
    Thumbnails = 1,

    /// <summary>Spine strip images (7×100 JPEG).</summary>
    Spines = 2,

    /// <summary>hOCR output files (populated by Phase 15).</summary>
    Ocr = 3,

    /// <summary>Gzip-compressed extracted text pages (JSON).</summary>
    ExtractedText = 4,

    /// <summary>Raw embedding vector files (populated by Phase 11).</summary>
    Embeddings = 5,

    /// <summary>Pre-write-back PDF backups.</summary>
    Backups = 6,

    /// <summary>Export bundle archives.</summary>
    Export = 7,

    /// <summary>Plain-text citation exports.</summary>
    CitationExports = 8,
}

/// <summary>
/// Resolves and provisions canonical sidecar asset paths for a book, using the
/// SHA-256 sharded layout defined in Phase 04 §7 (ADR-0005).
/// </summary>
/// <remarks>
/// <para>
/// The layout under the library root is:
/// <c>.ogma/&lt;class&gt;/&lt;hash[0:2]&gt;/&lt;hash&gt;.&lt;ext&gt;</c>
/// where <c>hash</c> is the SHA-256 hex digest of the book's content.
/// </para>
/// <para>
/// All paths returned for DB storage use forward-slash separators, regardless of the
/// host OS, so the catalogue is fully portable (NFR-PROD-009).
/// </para>
/// <para>
/// Directories are created on demand at first resolve; the method never silently fails.
/// </para>
/// </remarks>
public interface ISidecarService
{
    /// <summary>
    /// Resolves the absolute path for a sidecar asset of the given class, creating
    /// any missing parent directories. Use this path for file I/O.
    /// </summary>
    /// <param name="contentHash">The SHA-256 hex digest identifying the book's content.</param>
    /// <param name="sidecarClass">The class of sidecar asset.</param>
    /// <param name="variant">
    /// An optional variant suffix appended before the extension
    /// (e.g. <c>"_p003"</c> for page thumbnails, <c>"_mymodel"</c> for embeddings).
    /// </param>
    /// <returns>The absolute, OS-native path to the sidecar asset file.</returns>
    string Resolve(string contentHash, SidecarClass sidecarClass, string? variant = null);

    /// <summary>
    /// Resolves the relative forward-slash path for a sidecar asset for storage in
    /// the catalogue database. Never absolute; always portable across OS.
    /// </summary>
    /// <param name="contentHash">The SHA-256 hex digest identifying the book's content.</param>
    /// <param name="sidecarClass">The class of sidecar asset.</param>
    /// <param name="variant">
    /// An optional variant suffix appended before the extension.
    /// </param>
    /// <returns>A forward-slash relative path, e.g. <c>.ogma/covers/ab/abcdef….jpg</c>.</returns>
    string ResolveRelative(string contentHash, SidecarClass sidecarClass, string? variant = null);
}
