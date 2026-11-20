using System.Security.Cryptography;

namespace OgmaLibrary.Infrastructure.AI.Onnx;

/// <summary>Installation state of the built-in embedding model.</summary>
public enum EmbeddingModelStatus
{
    /// <summary>One or more model files are absent.</summary>
    NotInstalled = 0,

    /// <summary>A file is present but its size or SHA-256 does not match the manifest.</summary>
    Corrupt = 1,

    /// <summary>Every file is present and matches the manifest.</summary>
    Ready = 2,
}

/// <summary>
/// Local folder of the built-in embedding model, under the app-data store at
/// <c>&lt;data&gt;/models/embeddings/&lt;model&gt;/&lt;revision&gt;/</c>, never in a
/// library folder or in git. Verification streams each file through SHA-256
/// on a worker thread and never touches the network.
/// </summary>
public sealed class EmbeddingModelStore
{
    private const int HashBufferSize = 1024 * 1024;

    /// <summary>Initializes a store under the app-data directory.</summary>
    /// <param name="dataDirectory">The app-data directory (the folder holding <c>catalogue.db</c>).</param>
    /// <param name="manifest">The pinned model manifest.</param>
    public EmbeddingModelStore(string dataDirectory, EmbeddingModelManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ModelDirectory = Path.GetFullPath(Path.Combine(
            RequireDirectory(dataDirectory),
            "models",
            "embeddings",
            manifest.FolderName,
            manifest.Revision));
        Manifest = manifest;
    }

    private EmbeddingModelStore(EmbeddingModelManifest manifest, string modelDirectory)
    {
        ModelDirectory = Path.GetFullPath(modelDirectory);
        Manifest = manifest;
    }

    /// <summary>Raised when the installed files may have changed and cached state must be dropped.</summary>
    public event EventHandler? Changed;

    /// <summary>The pinned manifest this store verifies against.</summary>
    public EmbeddingModelManifest Manifest { get; }

    /// <summary>Absolute folder holding the model files.</summary>
    public string ModelDirectory { get; }

    /// <summary>
    /// Creates a store over an explicit model folder (a verified test fixture
    /// or a staging folder) instead of the app-data location.
    /// </summary>
    public static EmbeddingModelStore ForModelDirectory(string modelDirectory, EmbeddingModelManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new EmbeddingModelStore(manifest, RequireDirectory(modelDirectory));
    }

    /// <summary>Absolute path of the file with <paramref name="role"/>.</summary>
    public string GetFilePath(EmbeddingModelFileRole role) => GetFilePath(Manifest.GetFile(role));

    /// <summary>Absolute path of <paramref name="file"/> inside <see cref="ModelDirectory"/>.</summary>
    public string GetFilePath(EmbeddingModelFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string path = Path.GetFullPath(Path.Combine(ModelDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar)));
        string root = ModelDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? ModelDirectory
            : ModelDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A model file resolved outside the model folder.");
        }

        return path;
    }

    /// <summary>
    /// Verifies every file against the manifest off the calling thread.
    /// Returns <see cref="EmbeddingModelStatus.NotInstalled"/> when a file is
    /// missing, <see cref="EmbeddingModelStatus.Corrupt"/> when a size or hash
    /// differs or a file cannot be read, otherwise <see cref="EmbeddingModelStatus.Ready"/>.
    /// </summary>
    public Task<EmbeddingModelStatus> VerifyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => VerifyAsyncCore(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Signals that the files changed (installed, replaced or erased), so
    /// consumers drop cached availability and loaded sessions.
    /// </summary>
    public void Invalidate() => Changed?.Invoke(this, EventArgs.Empty);

    private async Task<EmbeddingModelStatus> VerifyAsyncCore(CancellationToken cancellationToken)
    {
        foreach (EmbeddingModelFile file in Manifest.Files)
        {
            if (!File.Exists(GetFilePath(file)))
            {
                return EmbeddingModelStatus.NotInstalled;
            }
        }

        foreach (EmbeddingModelFile file in Manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await MatchesAsync(GetFilePath(file), file, cancellationToken).ConfigureAwait(false))
            {
                return EmbeddingModelStatus.Corrupt;
            }
        }

        return EmbeddingModelStatus.Ready;
    }

    private static async Task<bool> MatchesAsync(string path, EmbeddingModelFile file, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                HashBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (stream.ConfigureAwait(false))
            {
                if (stream.Length != file.SizeBytes)
                {
                    return false;
                }

                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                return string.Equals(Convert.ToHexStringLower(hash), file.Sha256, StringComparison.Ordinal);
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string RequireDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return directory;
    }
}
