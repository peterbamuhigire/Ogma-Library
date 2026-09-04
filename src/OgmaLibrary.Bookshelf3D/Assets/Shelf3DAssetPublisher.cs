using System.Security.Cryptography;
using System.Text.Json;

namespace OgmaLibrary.Bookshelf3D.Assets;

/// <summary>Publishes bundled Three.js shelf assets into the local <c>ogma://</c> asset root.</summary>
public sealed class Shelf3DAssetPublisher
{
    private static readonly string[] RequiredFiles = ["index.html", "shelf3d.js", "shelf3d.build.json"];
    private readonly string _sourceRoot;

    /// <summary>Initializes a publisher from the default application output directory.</summary>
    public Shelf3DAssetPublisher()
        : this(Path.Combine(AppContext.BaseDirectory, "shelf3d"))
    {
    }

    /// <summary>Initializes a publisher from an explicit source root, primarily for tests.</summary>
    /// <param name="sourceRoot">Directory containing the built web assets.</param>
    public Shelf3DAssetPublisher(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        _sourceRoot = Path.GetFullPath(sourceRoot);
    }

    /// <summary>Copies the built web assets into <c>{assetRoot}/js</c>.</summary>
    /// <param name="assetRoot">Root served by <see cref="OgmaSchemeHandler"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <c>ogma://</c> URI for the bootstrap document.</returns>
    public async Task<Uri> PublishAsync(string assetRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);

        string sourceRoot = Path.GetFullPath(_sourceRoot);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Shelf3D asset source directory was not found: {sourceRoot}");
        }

        string jsRoot = Path.Combine(Path.GetFullPath(assetRoot), "js");
        Directory.CreateDirectory(jsRoot);

        await ValidateBuildManifestAsync(sourceRoot, cancellationToken).ConfigureAwait(false);

        foreach (string fileName in RequiredFiles)
        {
            string sourcePath = Path.Combine(sourceRoot, fileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Required Shelf3D asset was not found: {fileName}", sourcePath);
            }

            string destinationPath = Path.Combine(jsRoot, fileName);
            using FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = File.Open(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return new Uri("ogma://assets/js/index.html");
    }

    private static async Task ValidateBuildManifestAsync(string sourceRoot, CancellationToken cancellationToken)
    {
        string manifestPath = Path.Combine(sourceRoot, "shelf3d.build.json");
        string bundlePath = Path.Combine(sourceRoot, "shelf3d.js");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Shelf3D build provenance manifest was not found.", manifestPath);
        }

        using FileStream stream = File.OpenRead(manifestPath);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        JsonElement root = document.RootElement;
        if (!string.Equals(ReadRequiredString(root, "schema"), "ogma-shelf3d-build-v1", StringComparison.Ordinal) ||
            !string.Equals(ReadRequiredString(root, "entryPoint"), "src/main.ts", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Shelf3D build provenance manifest has an unsupported schema or entry point.");
        }

        if (!root.TryGetProperty("sourceFiles", out JsonElement sourceFiles) ||
            sourceFiles.ValueKind != JsonValueKind.Array ||
            !sourceFiles.EnumerateArray().Any(file =>
                file.ValueKind == JsonValueKind.String &&
                string.Equals(file.GetString(), "src/main.ts", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Shelf3D build provenance must list the TypeScript entry point.");
        }

        _ = ReadRequiredHash(root, "sourceSha256");
        _ = ReadRequiredHash(root, "lockfileSha256");
        string declaredBundleHash = ReadRequiredHash(root, "bundleSha256");
        using FileStream bundleStream = File.OpenRead(bundlePath);
        string actualBundleHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(
            bundleStream, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(declaredBundleHash, actualBundleHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Shelf3D build provenance does not match the packaged bundle.");
        }
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Shelf3D build provenance property '{propertyName}' is required.");
        }

        return value.GetString()!;
    }

    private static string ReadRequiredHash(JsonElement root, string propertyName)
    {
        string value = ReadRequiredString(root, propertyName);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"Shelf3D build provenance property '{propertyName}' must be a SHA-256 digest.");
        }

        return value.ToLowerInvariant();
    }
}
