namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Semaphore-backed page-render concurrency limiter.</summary>
internal sealed class LanPageRenderLimiter : ILanPageRenderLimiter, IDisposable
{
    public const int DefaultMaxConcurrentRenders = 10;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public LanPageRenderLimiter()
        : this(DefaultMaxConcurrentRenders)
    {
    }

    internal LanPageRenderLimiter(int maxConcurrentRenders)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentRenders);
        _semaphore = new SemaphoreSlim(maxConcurrentRenders, maxConcurrentRenders);
    }

    /// <inheritdoc />
    public bool TryAcquire(out IDisposable lease)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_semaphore.Wait(0))
        {
            lease = NoopLease.Instance;
            return false;
        }

        lease = new SemaphoreLease(_semaphore);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _semaphore.Dispose();
    }

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }

    private sealed class NoopLease : IDisposable
    {
        public static readonly NoopLease Instance = new();

        public void Dispose()
        {
        }
    }
}
