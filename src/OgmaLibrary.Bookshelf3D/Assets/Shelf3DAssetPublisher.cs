namespace OgmaLibrary.Bookshelf3D.Assets;

/// <summary>Publishes bundled Three.js shelf assets into the local <c>ogma://</c> asset root.</summary>
public sealed class Shelf3DAssetPublisher
{
    private static readonly string[] RequiredFiles = ["index.html", "shelf3d.js"];
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
}
