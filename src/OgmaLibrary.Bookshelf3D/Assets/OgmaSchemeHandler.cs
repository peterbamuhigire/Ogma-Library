namespace OgmaLibrary.Bookshelf3D.Assets;

/// <summary>Serves local 3D shelf assets through the safe <c>ogma://</c> scheme.</summary>
public sealed class OgmaSchemeHandler : ISchemeHandler
{
    private static readonly HashSet<string> AllowedAssetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "covers",
        "spines",
        "thumbnails",
        "js",
    };

    private readonly string _assetRoot;

    /// <summary>Initializes a new instance of <see cref="OgmaSchemeHandler"/>.</summary>
    /// <param name="assetRoot">Absolute root that contains asset class subdirectories.</param>
    public OgmaSchemeHandler(string assetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        _assetRoot = Path.GetFullPath(assetRoot);
    }

    /// <inheritdoc />
    public bool CanHandle(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.Scheme.Equals("ogma", StringComparison.OrdinalIgnoreCase) &&
            uri.Host.Equals("assets", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<SchemeResponse> HandleAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!CanHandle(uri))
        {
            return SchemeResponse.NotFound;
        }

        AssetRequest request = ParseAssetRequest(uri);
        if (!request.IsSafe)
        {
            return SchemeResponse.Forbidden;
        }

        if (!AllowedAssetClasses.Contains(request.AssetClass))
        {
            return SchemeResponse.NotFound;
        }

        string classRoot = Path.GetFullPath(Path.Combine(_assetRoot, request.AssetClass));
        string resolvedPath = Path.GetFullPath(Path.Combine(classRoot, request.FileName));
        if (!IsInsideRoot(resolvedPath, classRoot))
        {
            return SchemeResponse.Forbidden;
        }

        if (!File.Exists(resolvedPath))
        {
            return SchemeResponse.NotFound;
        }

        byte[] body = await File.ReadAllBytesAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        return new SchemeResponse(200, GetContentType(resolvedPath), body);
    }

    private static AssetRequest ParseAssetRequest(Uri uri)
    {
        string prefix = $"{uri.Scheme}://{uri.Host}/";
        string original = uri.OriginalString;
        if (!original.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return AssetRequest.Unsafe;
        }

        string rawPath = original[prefix.Length..];
        int queryIndex = rawPath.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            rawPath = rawPath[..queryIndex];
        }

        string[] rawSegments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (rawSegments.Length != 2)
        {
            return AssetRequest.Unsafe;
        }

        string assetClass = Uri.UnescapeDataString(rawSegments[0]);
        string fileName = Uri.UnescapeDataString(rawSegments[1]);
        if (IsUnsafeSegment(assetClass) || IsUnsafeSegment(fileName) || Path.GetFileName(fileName) != fileName)
        {
            return AssetRequest.Unsafe;
        }

        return new AssetRequest(true, assetClass, fileName);
    }

    private static bool IsUnsafeSegment(string segment) =>
        string.IsNullOrWhiteSpace(segment) ||
        segment is "." or ".." ||
        segment.Contains('\\', StringComparison.Ordinal) ||
        segment.Contains('/', StringComparison.Ordinal);

    private static bool IsInsideRoot(string resolvedPath, string root)
    {
        string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return resolvedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".js" => "application/javascript",
            ".json" => "application/json",
            _ => "application/octet-stream",
        };

    private readonly record struct AssetRequest(bool IsSafe, string AssetClass, string FileName)
    {
        public static AssetRequest Unsafe { get; } = new(false, string.Empty, string.Empty);
    }
}
