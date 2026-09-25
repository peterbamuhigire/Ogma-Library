using System.Collections.Concurrent;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Scheduling class of a request sent to a persistent PDF worker session. The worker
/// is single-threaded, so the client decides the order: cheap control requests
/// (geometry, rotation, metadata) go first, then low-resolution previews, then full
/// renders (Sept-23 Kaizen K32: a page turn must not wait behind queued prefetches).
/// </summary>
internal enum WorkerRequestPriority
{
    /// <summary>Geometry, rotation, metadata, outline and text-layer requests.</summary>
    Control = 0,

    /// <summary>Low-resolution preview renders.</summary>
    Preview = 1,

    /// <summary>Full-resolution renders, including prefetches.</summary>
    Render = 2,
}

/// <summary>
/// A single-holder asynchronous gate whose waiters are admitted by priority and then
/// in arrival order. Cancelling a queued waiter removes it from the queue, which is how
/// stale prefetch renders are dropped before they ever reach the worker.
/// </summary>
internal sealed class PriorityRequestGate : IDisposable
{
    private static readonly int PriorityCount = Enum.GetValues<WorkerRequestPriority>().Length;

    private readonly Lock _sync = new();
    private readonly LinkedList<Waiter>[] _queues;
    private bool _held;
    private bool _disposed;

    /// <summary>Initializes an unheld gate.</summary>
    public PriorityRequestGate()
    {
        _queues = new LinkedList<Waiter>[PriorityCount];
        for (int index = 0; index < PriorityCount; index++)
        {
            _queues[index] = new LinkedList<Waiter>();
        }
    }

    /// <summary>Gets the number of queued waiters.</summary>
    public int QueueDepth
    {
        get
        {
            lock (_sync)
            {
                return _queues.Sum(queue => queue.Count);
            }
        }
    }

    /// <summary>Waits for exclusive access to the worker pipe.</summary>
    /// <param name="priority">The scheduling class of the request.</param>
    /// <param name="cancellationToken">Cancels the wait and removes the waiter from the queue.</param>
    /// <returns>A task that completes when the caller holds the gate.</returns>
    public Task WaitAsync(WorkerRequestPriority priority, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LinkedListNode<Waiter> node;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_held)
            {
                _held = true;
                return Task.CompletedTask;
            }

            node = _queues[(int)priority].AddLast(new Waiter());
        }

        if (cancellationToken.CanBeCanceled)
        {
            node.Value.Registration = cancellationToken.Register(
                static state =>
                {
                    var (gate, waiterNode, token) = ((PriorityRequestGate, LinkedListNode<Waiter>, CancellationToken))state!;
                    gate.CancelWaiter(waiterNode, token);
                },
                (this, node, cancellationToken));
        }

        return node.Value.Completion.Task;
    }

    /// <summary>Passes the gate to the highest-priority waiter, or frees it.</summary>
    public void Release()
    {
        while (true)
        {
            Waiter? next = null;
            lock (_sync)
            {
                if (_disposed)
                {
                    _held = false;
                    return;
                }

                foreach (LinkedList<Waiter> queue in _queues)
                {
                    if (queue.First is { } first)
                    {
                        queue.RemoveFirst();
                        next = first.Value;
                        break;
                    }
                }

                if (next is null)
                {
                    _held = false;
                    return;
                }
            }

            next.Registration.Dispose();
            if (next.Completion.TrySetResult())
            {
                return;
            }

            // The waiter was already completed (cancelled concurrently); try the next one.
        }
    }

    /// <summary>Fails every queued waiter and refuses new waiters.</summary>
    public void Dispose()
    {
        List<Waiter> abandoned = [];
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (LinkedList<Waiter> queue in _queues)
            {
                abandoned.AddRange(queue);
                queue.Clear();
            }
        }

        foreach (Waiter waiter in abandoned)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetException(new ObjectDisposedException(nameof(PriorityRequestGate)));
        }
    }

    private void CancelWaiter(LinkedListNode<Waiter> node, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (node.List is null)
            {
                return;
            }

            node.List.Remove(node);
        }

        node.Value.Completion.TrySetCanceled(cancellationToken);
    }

    private sealed class Waiter
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration Registration { get; set; }
    }
}

/// <summary>
/// Correlates worker responses with requests by a monotonically increasing request id.
/// A response whose request already timed out or was abandoned is discarded instead of
/// being consumed by the next request (Sept-23 Kaizen K32 protocol desynchronisation).
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class WorkerResponseRouter<TResponse>
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<TResponse>> _pending = new();
    private long _nextRequestId;
    private long _discarded;
    private Exception? _fault;

    /// <summary>Gets the number of late or unknown responses that were discarded.</summary>
    public long DiscardedCount => Interlocked.Read(ref _discarded);

    /// <summary>Gets the number of requests awaiting a response.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Allocates the next request id.</summary>
    /// <returns>A positive, unique request id.</returns>
    public long NextRequestId() => Interlocked.Increment(ref _nextRequestId);

    /// <summary>Registers a request and returns the task that receives its response.</summary>
    /// <param name="requestId">The request id sent to the worker.</param>
    /// <returns>A task completed by <see cref="TryComplete"/> or <see cref="FailAll"/>.</returns>
    public Task<TResponse> Register(long requestId)
    {
        var completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = completion;

        // FailAll may have swept the table between the caller's health check and this
        // registration; observe the fault so the request cannot hang.
        if (Volatile.Read(ref _fault) is { } fault && _pending.TryRemove(requestId, out _))
        {
            completion.TrySetException(fault);
        }

        return completion.Task;
    }

    /// <summary>Delivers a response to its request.</summary>
    /// <param name="requestId">The id echoed by the worker.</param>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> when a waiting request received it; otherwise it was discarded.</returns>
    public bool TryComplete(long requestId, TResponse response)
    {
        if (_pending.TryRemove(requestId, out TaskCompletionSource<TResponse>? completion) &&
            completion.TrySetResult(response))
        {
            return true;
        }

        Interlocked.Increment(ref _discarded);
        return false;
    }

    /// <summary>Stops waiting for a request, so its late response will be discarded.</summary>
    /// <param name="requestId">The request id.</param>
    public void Abandon(long requestId) => _pending.TryRemove(requestId, out _);

    /// <summary>Fails every pending and future request with <paramref name="fault"/>.</summary>
    /// <param name="fault">The terminal session failure.</param>
    public void FailAll(Exception fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        Interlocked.CompareExchange(ref _fault, fault, null);
        foreach (long requestId in _pending.Keys)
        {
            if (_pending.TryRemove(requestId, out TaskCompletionSource<TResponse>? completion))
            {
                completion.TrySetException(fault);
            }
        }
    }
}

/// <summary>
/// The persistent PDF worker session is no longer usable: its process exited, its pipe
/// broke, or a request exceeded the per-request wall clock and the worker was killed.
/// The reader supervisor responds by starting a replacement session.
/// </summary>
public sealed class PdfWorkerSessionLostException : IOException
{
    /// <summary>Initializes the exception with a stable reason code.</summary>
    /// <param name="reason">A content-free reason code such as <c>worker_exited</c>.</param>
    public PdfWorkerSessionLostException(string reason)
        : base($"The PDF worker session was lost ({reason}).")
    {
        Reason = reason;
    }

    /// <summary>Initializes the exception with a stable reason code and cause.</summary>
    /// <param name="reason">A content-free reason code.</param>
    /// <param name="innerException">The underlying failure.</param>
    public PdfWorkerSessionLostException(string reason, Exception innerException)
        : base($"The PDF worker session was lost ({reason}).", innerException)
    {
        Reason = reason;
    }

    /// <summary>Gets the stable, content-free reason code.</summary>
    public string Reason { get; }
}

/// <summary>
/// Resource policy for persistent interactive reader sessions. Unlike one-shot jobs,
/// sessions have no cumulative CPU cap (it killed healthy reading sessions, K30);
/// runaway work is bounded by the per-request wall clock, the unchanged memory cap,
/// the idle timeout and the respawn budget.
/// </summary>
public sealed class PdfWorkerSessionLimits
{
    /// <summary>Wall clock for one request; exceeding it kills and replaces the worker.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Time allowed for a worker to start and load the document.</summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Idle time after which the worker is closed; the next request restarts it.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Maximum worker replacements within <see cref="RespawnWindow"/> before giving up.</summary>
    public int MaxRespawns { get; set; } = 3;

    /// <summary>The sliding window for <see cref="MaxRespawns"/>.</summary>
    public TimeSpan RespawnWindow { get; set; } = TimeSpan.FromSeconds(60);
}
