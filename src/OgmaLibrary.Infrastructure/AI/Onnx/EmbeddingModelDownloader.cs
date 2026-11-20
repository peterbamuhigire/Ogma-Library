using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.AI.Onnx;

/// <summary>
/// The one network adapter for the built-in embedding model (Sept-23 Phase 14,
/// D-103, D-104). It runs only when the user asks for the download, fetches the
/// pinned files over HTTPS from the manifest's host allowlist (redirects are
/// followed by hand so every hop is checked), writes <c>*.partial</c> files that
/// resume with <c>Range</c>, checks free space first and moves a file into place
/// only after its size and SHA-256 match the manifest.
/// </summary>
public sealed class EmbeddingModelDownloader : IEmbeddingModelInstaller, IDisposable
{
    /// <summary>Name of the typed client; its primary handler must not follow redirects.</summary>
    public const string HttpClientName = "ogma-embedding-model";

    private const string PartialSuffix = ".partial";
    private const int MaxRedirects = 5;
    private const int CopyBufferSize = 81920;
    private const long SpaceHeadroomBytes = 16L * 1024 * 1024;
    private const string Component = nameof(EmbeddingModelDownloader);

    private readonly HttpClient _httpClient;
    private readonly EmbeddingModelStore _store;
    private readonly ILogger<EmbeddingModelDownloader> _logger;
    private readonly Func<string, long> _freeSpace;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the downloader.</summary>
    /// <param name="httpClient">Client whose handler does not follow redirects automatically.</param>
    /// <param name="store">The model store the files are installed into.</param>
    /// <param name="logger">Redacting logger (Sept-23 Phase 02).</param>
    /// <param name="freeSpace">Returns free bytes for a directory; tests substitute it.</param>
    public EmbeddingModelDownloader(
        HttpClient httpClient,
        EmbeddingModelStore store,
        ILogger<EmbeddingModelDownloader> logger,
        Func<string, long>? freeSpace = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _store = store;
        _logger = logger;
        _freeSpace = freeSpace ?? ReadFreeSpace;
    }

    private EmbeddingModelManifest Manifest => _store.Manifest;

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private long TotalBytes => Manifest.Files.Sum(file => file.SizeBytes);

    /// <inheritdoc />
    public async Task<EmbeddingModelInstallStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        EmbeddingModelStatus verified = await _store.VerifyAsync(cancellationToken).ConfigureAwait(false);
        EmbeddingModelInstallState state = verified switch
        {
            EmbeddingModelStatus.Ready => EmbeddingModelInstallState.Installed,
            EmbeddingModelStatus.Corrupt => EmbeddingModelInstallState.Corrupt,
            _ => EmbeddingModelInstallState.NotInstalled,
        };
        long remaining = state == EmbeddingModelInstallState.Installed ? 0 : RemainingBytes();
        return new EmbeddingModelInstallStatus(
            state,
            Manifest.ModelId,
            Manifest.Revision,
            Manifest.Licence,
            remaining,
            TotalBytes);
    }

    /// <inheritdoc />
    public async Task<EmbeddingModelInstallResult> InstallAsync(
        IProgress<EmbeddingModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EmbeddingModelInstallOutcome outcome = await InstallCoreAsync(progress, cancellationToken).ConfigureAwait(false);
            InfrastructureLog.EmbeddingModelInstallFinished(_logger, outcome);
            return new EmbeddingModelInstallResult(outcome);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_store.ModelDirectory))
            {
                Directory.Delete(_store.ModelDirectory, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }

        _store.Invalidate();
    }

    private async Task<EmbeddingModelInstallOutcome> InstallCoreAsync(
        IProgress<EmbeddingModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (await _store.VerifyAsync(cancellationToken).ConfigureAwait(false) == EmbeddingModelStatus.Ready)
        {
            progress?.Report(new EmbeddingModelInstallProgress(TotalBytes, TotalBytes));
            return EmbeddingModelInstallOutcome.Installed;
        }

        Directory.CreateDirectory(_store.ModelDirectory);
        long needed = RemainingBytes();
        if (needed > 0 && _freeSpace(_store.ModelDirectory) < needed + SpaceHeadroomBytes)
        {
            return EmbeddingModelInstallOutcome.InsufficientSpace;
        }

        var tracker = new ProgressTracker(progress, TotalBytes);
        foreach (EmbeddingModelFile file in Manifest.Files)
        {
            EmbeddingModelInstallOutcome? failure = await InstallFileAsync(file, tracker, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure.Value;
            }
        }

        _store.Invalidate();
        return EmbeddingModelInstallOutcome.Installed;
    }

    private async Task<EmbeddingModelInstallOutcome?> InstallFileAsync(
        EmbeddingModelFile file,
        ProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        string target = _store.GetFilePath(file);
        if (File.Exists(target) && await MatchesAsync(target, file, cancellationToken).ConfigureAwait(false))
        {
            tracker.Add(file.SizeBytes);
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string partial = target + PartialSuffix;
        long existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (existing > file.SizeBytes)
        {
            File.Delete(partial);
            existing = 0;
        }

        tracker.Add(existing);
        if (existing < file.SizeBytes)
        {
            EmbeddingModelInstallOutcome? failure = await DownloadAsync(file, partial, existing, tracker, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        if (!await MatchesAsync(partial, file, cancellationToken).ConfigureAwait(false))
        {
            File.Delete(partial);
            return EmbeddingModelInstallOutcome.ChecksumMismatch;
        }

        File.Move(partial, target, overwrite: true);
        return null;
    }

    private async Task<EmbeddingModelInstallOutcome?> DownloadAsync(
        EmbeddingModelFile file,
        string partial,
        long existing,
        ProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(Manifest.Source, file.Path);
        HttpResponseMessage? response = null;
        try
        {
            for (int hop = 0; ; hop++)
            {
                if (!IsAllowed(uri))
                {
                    return EmbeddingModelInstallOutcome.HostNotAllowed;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (existing > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(existing, null);
                }

                response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!IsRedirect(response.StatusCode))
                {
                    break;
                }

                Uri? location = response.Headers.Location;
                response.Dispose();
                response = null;
                if (location is null || hop >= MaxRedirects)
                {
                    return EmbeddingModelInstallOutcome.ServerError;
                }

                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
            }

            bool append;
            if (response.StatusCode == HttpStatusCode.PartialContent && existing > 0
                && response.Content.Headers.ContentRange?.From == existing)
            {
                append = true;
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                // The server ignored the range: start the file again.
                tracker.Add(-existing);
                append = false;
            }
            else
            {
                return EmbeddingModelInstallOutcome.ServerError;
            }

            return await WriteAsync(response, file, partial, append, tracker, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            InfrastructureLog.DegradedStep(_logger, exception, Component, "embedding.model.download");
            return EmbeddingModelInstallOutcome.Offline;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout, not the user's cancellation.
            InfrastructureLog.DegradedStep(_logger, exception, Component, "embedding.model.download.timeout");
            return EmbeddingModelInstallOutcome.Offline;
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task<EmbeddingModelInstallOutcome?> WriteAsync(
        HttpResponseMessage response,
        EmbeddingModelFile file,
        string partial,
        bool append,
        ProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        try
        {
            Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var destination = new FileStream(
                    partial,
                    append ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    CopyBufferSize,
                    FileOptions.Asynchronous);
                await using (destination.ConfigureAwait(false))
                {
                    byte[] buffer = new byte[CopyBufferSize];
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        if (destination.Length + read > file.SizeBytes)
                        {
                            // More bytes than pinned: the payload is not the pinned file.
                            await destination.DisposeAsync().ConfigureAwait(false);
                            File.Delete(partial);
                            return EmbeddingModelInstallOutcome.ChecksumMismatch;
                        }

                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        tracker.Add(read);
                    }
                }
            }

            return null;
        }
        catch (IOException exception) when (IsDiskFull(exception))
        {
            InfrastructureLog.DegradedStep(_logger, exception, Component, "embedding.model.write");
            return EmbeddingModelInstallOutcome.InsufficientSpace;
        }
    }

    private bool IsAllowed(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && Manifest.AllowedDownloadHosts.Contains(uri.IdnHost.ToLowerInvariant(), StringComparer.Ordinal);

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool IsDiskFull(IOException exception)
    {
        const int ErrorHandleDiskFull = unchecked((int)0x80070027);
        const int ErrorDiskFull = unchecked((int)0x80070070);
        const int PosixNoSpace = 28;
        return exception.HResult is ErrorHandleDiskFull or ErrorDiskFull or PosixNoSpace;
    }

    private long RemainingBytes()
    {
        long remaining = 0;
        foreach (EmbeddingModelFile file in Manifest.Files)
        {
            string target = _store.GetFilePath(file);
            if (File.Exists(target) && new FileInfo(target).Length == file.SizeBytes)
            {
                continue;
            }

            string partial = target + PartialSuffix;
            long present = File.Exists(partial) ? new FileInfo(partial).Length : 0;
            remaining += Math.Max(0, file.SizeBytes - present);
        }

        return remaining;
    }

    private static async Task<bool> MatchesAsync(string path, EmbeddingModelFile file, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
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

    private static long ReadFreeSpace(string directory)
    {
        string root = Path.GetPathRoot(Path.GetFullPath(directory)) ?? directory;
        return new DriveInfo(root).AvailableFreeSpace;
    }

    private sealed class ProgressTracker(IProgress<EmbeddingModelInstallProgress>? progress, long total)
    {
        private long _received;

        public void Add(long bytes)
        {
            _received += bytes;
            progress?.Report(new EmbeddingModelInstallProgress(_received, total));
        }
    }
}
