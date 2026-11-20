namespace OgmaLibrary.Application.Search;

/// <summary>Installation state of the built-in embedding model.</summary>
public enum EmbeddingModelInstallState
{
    /// <summary>No complete model is installed.</summary>
    NotInstalled = 0,

    /// <summary>A file is present but does not match the pinned size or SHA-256.</summary>
    Corrupt = 1,

    /// <summary>Every file is present and verified.</summary>
    Installed = 2,
}

/// <summary>
/// What the built-in model is and what installing it costs, so Settings can ask
/// for informed consent before any download (Sept-23 Phase 14, D-103, D-104).
/// </summary>
/// <param name="State">The verified installation state.</param>
/// <param name="ModelId">The model identifier.</param>
/// <param name="ModelVersion">The pinned revision.</param>
/// <param name="Licence">SPDX licence identifier of the weights.</param>
/// <param name="DownloadSizeBytes">Bytes still to download for a complete install (0 when installed).</param>
/// <param name="DiskUseBytes">Disk space the installed model occupies.</param>
public sealed record EmbeddingModelInstallStatus(
    EmbeddingModelInstallState State,
    string ModelId,
    string ModelVersion,
    string Licence,
    long DownloadSizeBytes,
    long DiskUseBytes)
{
    /// <summary>The one HTTPS host the download contacts first, stated to the user before consent.</summary>
    public string DownloadHost { get; init; } = string.Empty;
}

/// <summary>Terminal outcome of an install attempt. Cancellation throws <see cref="OperationCanceledException"/> and keeps partial files.</summary>
public enum EmbeddingModelInstallOutcome
{
    /// <summary>Every file was downloaded (or already present) and verified.</summary>
    Installed = 0,

    /// <summary>The download host could not be reached; the user can download later.</summary>
    Offline = 1,

    /// <summary>A downloaded file did not match its pinned SHA-256; it was deleted.</summary>
    ChecksumMismatch = 2,

    /// <summary>Not enough free disk space; nothing more was written.</summary>
    InsufficientSpace = 3,

    /// <summary>A URL or redirect pointed outside the pinned host allowlist or was not HTTPS.</summary>
    HostNotAllowed = 4,

    /// <summary>The server answered with an unexpected HTTP status.</summary>
    ServerError = 5,
}

/// <summary>Result of <see cref="IEmbeddingModelInstaller.InstallAsync"/>.</summary>
/// <param name="Outcome">The terminal outcome.</param>
public sealed record EmbeddingModelInstallResult(EmbeddingModelInstallOutcome Outcome)
{
    /// <summary>True when the model is installed and verified.</summary>
    public bool Succeeded => Outcome == EmbeddingModelInstallOutcome.Installed;
}

/// <summary>Download progress across every model file.</summary>
/// <param name="BytesReceived">Bytes present so far, including resumed partial bytes.</param>
/// <param name="TotalBytes">Total bytes of the model package.</param>
public sealed record EmbeddingModelInstallProgress(long BytesReceived, long TotalBytes);

/// <summary>
/// Installs, verifies and removes the built-in embedding model. Installing is
/// the only operation that touches the network, and only after the user has
/// explicitly asked for it (Sept-23 Phase 14, D-104). Implementations live in
/// Infrastructure; this contract carries no HTTP or ONNX types.
/// </summary>
public interface IEmbeddingModelInstaller
{
    /// <summary>Verifies the local files and reports the state with the declared download size and disk use. Never uses the network.</summary>
    /// <param name="cancellationToken">Cancels the verification.</param>
    /// <returns>The current status.</returns>
    Task<EmbeddingModelInstallStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Downloads, verifies and installs the pinned model files, resuming partial downloads.</summary>
    /// <param name="progress">Receives byte progress, or null.</param>
    /// <param name="cancellationToken">Cancels the download; partial files are kept for a later resume.</param>
    /// <returns>The terminal outcome.</returns>
    Task<EmbeddingModelInstallResult> InstallAsync(
        IProgress<EmbeddingModelInstallProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>Deletes the installed model and any partial download.</summary>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>A task that completes when the files are gone.</returns>
    Task RemoveAsync(CancellationToken cancellationToken);
}
