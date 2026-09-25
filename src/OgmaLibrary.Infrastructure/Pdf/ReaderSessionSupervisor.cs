using System.Diagnostics;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Supervises the persistent isolated worker behind one open document
/// (Sept-23 Kaizen T04.5). When the worker exits, its pipe breaks or a request exceeds
/// its wall clock, the next request transparently starts a replacement worker for the
/// same document (and password) and replays itself. More than
/// <see cref="PdfWorkerSessionLimits.MaxRespawns"/> recoveries within
/// <see cref="PdfWorkerSessionLimits.RespawnWindow"/> stop the supervisor, and callers
/// receive <see cref="PdfRendererUnavailableException"/>. An idle worker is closed after
/// <see cref="PdfWorkerSessionLimits.IdleTimeout"/> and restarted on demand without
/// counting against the respawn budget.
/// </summary>
internal sealed class ReaderSessionSupervisor : IDisposable
{
    /// <summary>A replayed request gets one retry after a recovery.</summary>
    private const int AttemptsPerRequest = 2;

    private readonly Func<char[]?, PdfWorkerClient.PdfWorkerSession> _openSession;
    private readonly PdfWorkerSessionLimits _limits;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Queue<long> _recentRespawns = new();
    private readonly ITimer? _idleTimer;
    private readonly char[]? _password;
    private PdfWorkerClient.PdfWorkerSession? _session;
    private string? _pendingFailureReason;
    private int _respawnCount;
    private int _idleCloseCount;
    private string _state = "Healthy";
    private bool _failed;
    private int _disposed;

    /// <summary>Opens the first worker session synchronously; failures propagate unchanged.</summary>
    /// <param name="openSession">Starts one worker session using the supplied password copy.</param>
    /// <param name="password">Optional password; a private copy is kept for respawns and cleared on dispose.</param>
    /// <param name="limits">The session resource policy.</param>
    /// <param name="timeProvider">Clock used for the respawn window and idle timeout.</param>
    public ReaderSessionSupervisor(
        Func<char[]?, PdfWorkerClient.PdfWorkerSession> openSession,
        char[]? password,
        PdfWorkerSessionLimits limits,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(openSession);
        ArgumentNullException.ThrowIfNull(limits);
        _openSession = openSession;
        _limits = limits;
        _time = timeProvider ?? TimeProvider.System;
        _password = password?.ToArray();
        try
        {
            _session = openSession(_password);
        }
        catch
        {
            ClearPassword();
            throw;
        }

        PageCount = _session.PageCount;
        TimeSpan period = TimeSpan.FromTicks(Math.Clamp(
            limits.IdleTimeout.Ticks / 4,
            TimeSpan.FromMilliseconds(250).Ticks,
            TimeSpan.FromSeconds(30).Ticks));
        _idleTimer = _time.CreateTimer(static state => ((ReaderSessionSupervisor)state!).CloseIfIdle(), this, period, period);
    }

    /// <summary>Raised after a lost worker was replaced.</summary>
    public event EventHandler<PdfRendererRecoveredEventArgs>? Recovered;

    /// <summary>Gets the page count reported when the document was opened.</summary>
    public int PageCount { get; }

    /// <summary>Gets the current worker session, for diagnostics and tests.</summary>
    internal PdfWorkerClient.PdfWorkerSession? CurrentSession => Volatile.Read(ref _session);

    /// <summary>Gets a content-free health snapshot.</summary>
    /// <returns>The current counters.</returns>
    public PdfRendererHealthSnapshot GetHealthSnapshot()
    {
        PdfWorkerClient.PdfWorkerSession? session = CurrentSession;
        return new PdfRendererHealthSnapshot(
            Volatile.Read(ref _state),
            Volatile.Read(ref _respawnCount),
            Volatile.Read(ref _idleCloseCount),
            session?.QueueDepth ?? 0,
            session?.DiscardedResponses ?? 0,
            session?.StandardErrorLines ?? 0);
    }

    /// <summary>
    /// Runs one worker operation, recovering the worker and replaying the operation once
    /// when the session is lost.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    /// <param name="operation">The operation to run against a healthy session.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<PdfWorkerClient.PdfWorkerSession, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for (int attempt = 1; ; attempt++)
        {
            PdfWorkerClient.PdfWorkerSession session = await AcquireSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return await operation(session, cancellationToken).ConfigureAwait(false);
            }
            catch (PdfWorkerSessionLostException) when (attempt < AttemptsPerRequest && !IsDisposed)
            {
                // The next iteration observes the faulted session and starts a replacement.
            }
        }
    }

    /// <summary>Stops the idle timer, the worker and clears the password copy.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _idleTimer?.Dispose();
        Interlocked.Exchange(ref _session, null)?.Dispose();
        Volatile.Write(ref _state, "Closed");
        ClearPassword();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private async Task<PdfWorkerClient.PdfWorkerSession> AcquireSessionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (Volatile.Read(ref _session) is { IsFaulted: false } healthy)
        {
            return healthy;
        }

        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        string reason;
        bool countsAsRecovery;
        PdfWorkerClient.PdfWorkerSession? lost;
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            lost = _session;
            if (lost is { IsFaulted: false })
            {
                return lost;
            }

            if (_failed)
            {
                throw new PdfRendererUnavailableException("The PDF reader engine stopped after repeated worker failures.");
            }

            reason = lost?.FaultReason ?? _pendingFailureReason ?? "idle_closed";
            countsAsRecovery = lost is not null || _pendingFailureReason is not null;
            if (countsAsRecovery && !TryReserveRespawn())
            {
                _failed = true;
                Volatile.Write(ref _state, "Failed");
                _session = null;
                DisposeInBackground(lost);
                throw new PdfRendererUnavailableException(
                    $"The PDF reader engine stopped after {_limits.MaxRespawns} worker recoveries within {_limits.RespawnWindow.TotalSeconds:F0} s.");
            }

            _session = null;
            Volatile.Write(ref _state, "Respawning");
            long started = _time.GetTimestamp();
            PdfWorkerClient.PdfWorkerSession replacement;
            try
            {
                // Tear down and start workers off the caller's thread: disposal waits for
                // the old process to exit and opening copies and hashes the document.
                replacement = await Task.Run(
                        () =>
                        {
                            lost?.Dispose();
                            return _openSession(_password);
                        },
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _pendingFailureReason = "respawn_failed";
                Volatile.Write(ref _state, "Respawning");
                throw new PdfRendererUnavailableException("The PDF reader engine could not be restarted.", exception);
            }

            TimeSpan elapsed = _time.GetElapsedTime(started);
            if (IsDisposed)
            {
                replacement.Dispose();
                throw new ObjectDisposedException(nameof(ReaderSessionSupervisor));
            }

            _session = replacement;
            _pendingFailureReason = null;
            Volatile.Write(ref _state, "Healthy");
            if (!countsAsRecovery)
            {
                return replacement;
            }

            int count = Interlocked.Increment(ref _respawnCount);
            RaiseRecovered(new PdfRendererRecoveredEventArgs(reason, count, elapsed));
            return replacement;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private bool TryReserveRespawn()
    {
        long now = _time.GetTimestamp();
        while (_recentRespawns.Count > 0 &&
               _time.GetElapsedTime(_recentRespawns.Peek(), now) > _limits.RespawnWindow)
        {
            _recentRespawns.Dequeue();
        }

        if (_recentRespawns.Count >= _limits.MaxRespawns)
        {
            return false;
        }

        _recentRespawns.Enqueue(now);
        return true;
    }

    private void CloseIfIdle()
    {
        if (IsDisposed ||
            Volatile.Read(ref _session) is not { IsFaulted: false, IsBusy: false } candidate ||
            _time.GetUtcNow().UtcDateTime - candidate.LastActivityUtc < _limits.IdleTimeout ||
            !_lifecycle.Wait(0))
        {
            return;
        }

        PdfWorkerClient.PdfWorkerSession? idle = null;
        try
        {
            if (!IsDisposed && ReferenceEquals(_session, candidate) && !candidate.IsBusy)
            {
                idle = candidate;
                _session = null;
                Interlocked.Increment(ref _idleCloseCount);
                Volatile.Write(ref _state, "Idle");
            }
        }
        finally
        {
            _lifecycle.Release();
        }

        idle?.Dispose();
    }

    private void RaiseRecovered(PdfRendererRecoveredEventArgs args)
    {
        EventHandler<PdfRendererRecoveredEventArgs>? handler = Recovered;
        if (handler is null)
        {
            return;
        }

        // Subscribers are diagnostics; a failing subscriber must not fail the page turn.
        foreach (EventHandler<PdfRendererRecoveredEventArgs> subscriber in handler.GetInvocationList().Cast<EventHandler<PdfRendererRecoveredEventArgs>>())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Trace.TraceWarning("reader.session.respawned subscriber failed: {0}", exception.GetType().Name);
            }
        }
    }

    private static void DisposeInBackground(PdfWorkerClient.PdfWorkerSession? session)
    {
        if (session is not null)
        {
            _ = Task.Run(session.Dispose);
        }
    }

    private void ClearPassword()
    {
        if (_password is not null)
        {
            Array.Clear(_password);
        }
    }
}
