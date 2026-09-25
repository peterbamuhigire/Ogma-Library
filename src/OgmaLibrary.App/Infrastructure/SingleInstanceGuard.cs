using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// One Ogma process per user and data directory (Sept-23 Phase 02, T02.6, K70). The first
/// process owns a named mutex (Windows) or an exclusive lock file (macOS and Linux) and listens
/// on a same-named pipe. A second launch sends an activation message over the pipe and exits
/// before it touches the database, workers or the LAN listener.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>The first line of every activation message.</summary>
    public const string ActivationHeader = "OGMA-ACTIVATE/1";

    private const int MaxPayloadChars = 8192;

    private readonly Mutex? _mutex;
    private readonly FileStream? _lockFile;
    private readonly CancellationTokenSource _listenerCancellation = new();
    private Task? _listener;
    private bool _disposed;

    private SingleInstanceGuard(string key, bool isPrimary, Mutex? mutex, FileStream? lockFile)
    {
        Key = key;
        IsPrimary = isPrimary;
        _mutex = mutex;
        _lockFile = lockFile;
    }

    /// <summary>The per-user, per-data-directory instance key (also the pipe name).</summary>
    public string Key { get; }

    /// <summary>Whether this process owns the instance.</summary>
    public bool IsPrimary { get; }

    /// <summary>Computes the instance key for <paramref name="dataDirectory"/>.</summary>
    /// <param name="dataDirectory">The Ogma data directory.</param>
    /// <returns><c>OgmaLibrary-&lt;sha256(path)[..16]&gt;</c>.</returns>
    public static string ComputeKey(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        string normalized = Path.GetFullPath(dataDirectory).TrimEnd('\\', '/');
        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToUpperInvariant();
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "OgmaLibrary-" + Convert.ToHexStringLower(digest)[..16];
    }

    /// <summary>Tries to become the primary instance for <paramref name="dataDirectory"/>.</summary>
    /// <param name="dataDirectory">The Ogma data directory.</param>
    /// <returns>A guard whose <see cref="IsPrimary"/> reports the outcome.</returns>
    public static SingleInstanceGuard Acquire(string dataDirectory)
    {
        string key = ComputeKey(dataDirectory);
        if (OperatingSystem.IsWindows())
        {
            var mutex = new Mutex(initiallyOwned: true, @"Local\" + key, out bool createdNew);
            if (createdNew)
            {
                return new SingleInstanceGuard(key, isPrimary: true, mutex, lockFile: null);
            }

            mutex.Dispose();
            return new SingleInstanceGuard(key, isPrimary: false, mutex: null, lockFile: null);
        }

        try
        {
            Directory.CreateDirectory(dataDirectory);
            var lockFile = new FileStream(
                Path.Combine(dataDirectory, ".ogma-instance.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            return new SingleInstanceGuard(key, isPrimary: true, mutex: null, lockFile);
        }
        catch (IOException)
        {
            return new SingleInstanceGuard(key, isPrimary: false, mutex: null, lockFile: null);
        }
    }

    /// <summary>
    /// Sends an activation message to the primary instance. Retries until <paramref name="timeout"/>
    /// so a primary that is still starting can pick it up.
    /// </summary>
    /// <param name="pdfPath">An optional PDF path for a future "Open with" hand-off.</param>
    /// <param name="timeout">The total time allowed.</param>
    /// <returns><see langword="true"/> when the primary received the message.</returns>
    public bool TrySignalPrimary(string? pdfPath, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            int remaining = (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    Key,
                    PipeDirection.Out,
                    PipeOptions.CurrentUserOnly);
                client.Connect(Math.Clamp(remaining, 1, 500));
                using var writer = new StreamWriter(client, new UTF8Encoding(false));
                writer.WriteLine(ActivationHeader);
                writer.WriteLine(pdfPath ?? string.Empty);
                writer.Flush();
                return true;
            }
            catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Starts listening for activation messages from later launches. Only the primary listens.
    /// </summary>
    /// <param name="onActivation">Called (on a background thread) with the optional PDF path.</param>
    /// <param name="logger">Optional logger.</param>
    public void StartListening(Action<string?> onActivation, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(onActivation);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary || _listener is not null)
        {
            return;
        }

        _listener = Task.Run(() => ListenAsync(onActivation, logger ?? NullLogger.Instance, _listenerCancellation.Token));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listenerCancellation.Cancel();
        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Intentionally ignored: the listener only ends by cancellation during shutdown.
        }

        _listenerCancellation.Dispose();
        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Intentionally ignored: released from another thread; the handle close frees it.
            }

            _mutex.Dispose();
        }

        _lockFile?.Dispose();
    }

    private async Task ListenAsync(Action<string?> onActivation, ILogger logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(
                    Key,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    char[] buffer = new char[MaxPayloadChars];
                    int read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    string[] lines = new string(buffer, 0, read).Split('\n');
                    if (lines.Length == 0 || lines[0].TrimEnd('\r') != ActivationHeader)
                    {
                        AppLog.ActivationRejected(logger);
                        continue;
                    }

                    string? path = lines.Length > 1 ? lines[1].TrimEnd('\r') : null;
                    AppLog.ActivationReceived(logger);
                    onActivation(string.IsNullOrWhiteSpace(path) ? null : path);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                AppLog.ActivationFailed(logger, exception);
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
