namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Resolves derived visual assets (covers, spines, thumbnails) to absolute paths
/// in the application-data asset store (Sept-23 Phase 05, D-04, ADR-0018).
/// Catalogue projections keep the portable <c>.ogma/&lt;class&gt;/…</c> key; this
/// locator is the only place that knows where those keys live on disk.
/// </summary>
public interface IAssetLocator
{
    /// <summary>The absolute root of the derived-asset store.</summary>
    string AssetStoreRoot { get; }

    /// <summary>
    /// Resolves a manifest-relative asset key to an absolute path inside the store,
    /// or returns null when the key is empty or unsafe.
    /// </summary>
    /// <param name="relativeAssetPath">The portable asset key, e.g. <c>.ogma/covers/ab/abcd.jpg</c>.</param>
    string? GetAbsolutePath(string? relativeAssetPath);
}
