using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Infrastructure.Sidecar;

/// <summary>
/// Resolves canonical sidecar asset paths using the SHA-256 sharded layout
/// (ADR-0005, Phase 04 WP5):
/// <c>&lt;libraryRoot&gt;/.ogma/&lt;class&gt;/&lt;hash[0:2]&gt;/&lt;hash&gt;[variant].&lt;ext&gt;</c>
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Resolve"/> returns absolute OS-native paths for file I/O.
/// <see cref="ResolveRelative"/> returns forward-slash relative paths for DB storage
/// (portable across Windows and macOS per NFR-PROD-009).
/// </para>
/// <para>
/// Directories are created on demand by <see cref="Resolve"/>; they are never
/// created by <see cref="ResolveRelative"/> (which is pure computation).
/// </para>
/// </remarks>
public sealed class SidecarService : ISidecarService
{
    private static readonly Dictionary<SidecarClass, (string Folder, string Extension)> ClassMeta
        = new()
        {
            [SidecarClass.Covers] = ("covers", "jpg"),
            [SidecarClass.Thumbnails] = ("thumbnails", "jpg"),
            [SidecarClass.Spines] = ("spines", "jpg"),
            [SidecarClass.Ocr] = ("ocr", "hocr"),
            [SidecarClass.ExtractedText] = ("text", "json.gz"),
            [SidecarClass.Embeddings] = ("embeddings", "bin"),
            [SidecarClass.Backups] = ("backups", "pdf"),
            [SidecarClass.Export] = ("export", "zip"),
            [SidecarClass.CitationExports] = ("citations", "txt"),
        };

    private readonly string _libraryRoot;

    /// <summary>
    /// Initializes a new instance of <see cref="SidecarService"/>.
    /// </summary>
    /// <param name="libraryRoot">The absolute path to the library root folder.</param>
    public SidecarService(string libraryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        _libraryRoot = libraryRoot;
    }

    /// <inheritdoc />
    public string Resolve(string contentHash, SidecarClass sidecarClass, string? variant = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        string relativePath = ResolveRelative(contentHash, sidecarClass, variant);
        // Convert forward-slash relative path to OS-native absolute path.
        string absolutePath = Path.Combine(
            _libraryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        // Ensure the parent directory exists (first-access creation).
        string? directory = Path.GetDirectoryName(absolutePath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return absolutePath;
    }

    /// <inheritdoc />
    public string ResolveRelative(string contentHash, SidecarClass sidecarClass, string? variant = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (!ClassMeta.TryGetValue(sidecarClass, out (string Folder, string Extension) meta))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sidecarClass),
                sidecarClass,
                "Unknown sidecar class.");
        }

        // Use the first two hex characters as the shard prefix.
        string prefix = contentHash.Length >= 2 ? contentHash[..2] : contentHash;
        string filename = $"{contentHash}{variant}{(variant is null ? "" : "")}.{meta.Extension}";

        // Always forward-slash for portability (NFR-PROD-009).
        return $".ogma/{meta.Folder}/{prefix}/{contentHash}{variant ?? string.Empty}.{meta.Extension}";
    }
}
