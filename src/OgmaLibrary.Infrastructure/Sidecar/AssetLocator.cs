using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.Sidecar;

/// <summary>
/// Resolves portable asset keys (<c>.ogma/&lt;class&gt;/&lt;shard&gt;/&lt;file&gt;</c>) against
/// the application-data asset store (Sept-23 Phase 05, D-04, ADR-0018). The store
/// root is the application data directory, so keys resolve to
/// <c>&lt;DataDirectory&gt;/.ogma/&lt;class&gt;/…</c>. Derived assets are content-addressed,
/// so one store serves every library root and nothing is written into the user's folders.
/// </summary>
public sealed class AssetLocator : IAssetLocator
{
    /// <summary>The portable key prefix kept in catalogue manifests.</summary>
    public const string KeyPrefix = ".ogma/";

    /// <summary>Initializes a locator for the given store root.</summary>
    /// <param name="assetStoreRoot">The absolute asset-store directory (normally the data directory).</param>
    public AssetLocator(string assetStoreRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetStoreRoot);
        AssetStoreRoot = PathGuard.CanonicalizeRoot(assetStoreRoot);
    }

    /// <inheritdoc />
    public string AssetStoreRoot { get; }

    /// <inheritdoc />
    public string? GetAbsolutePath(string? relativeAssetPath) =>
        ResolveKey(AssetStoreRoot, relativeAssetPath);

    /// <summary>
    /// Resolves an asset key against a store root, or returns null when the key is
    /// empty, rooted, outside <c>.ogma/</c> or tries to leave the store.
    /// </summary>
    /// <param name="storeRoot">The asset store root.</param>
    /// <param name="relativeAssetPath">The portable asset key.</param>
    public static string? ResolveKey(string storeRoot, string? relativeAssetPath)
    {
        if (string.IsNullOrWhiteSpace(storeRoot) || string.IsNullOrWhiteSpace(relativeAssetPath))
        {
            return null;
        }

        string key = relativeAssetPath.Trim().Replace('\\', '/');
        string[] segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase) ||
            segments.Length < 2 ||
            Path.IsPathRooted(key) ||
            segments.Any(segment => segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal)))
        {
            return null;
        }

        try
        {
            string root = PathGuard.CanonicalizeRoot(storeRoot);
            return PathGuard.EnsureWithinRoot(Path.Combine(root, Path.Combine(segments)), root);
        }
        catch (PathTraversalException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
