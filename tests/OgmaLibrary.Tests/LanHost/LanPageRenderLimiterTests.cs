using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 page-render concurrency limiter tests.</summary>
public sealed class LanPageRenderLimiterTests
{
    [Fact]
    public void TryAcquire_ReturnsFalse_WhenAllSlotsAreBusy()
    {
        using var limiter = new LanPageRenderLimiter(maxConcurrentRenders: 1);

        bool first = limiter.TryAcquire(out IDisposable lease);
        bool second = limiter.TryAcquire(out IDisposable saturatedLease);

        saturatedLease.Dispose();
        lease.Dispose();

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void TryAcquire_ReleasesSlot_WhenLeaseDisposed()
    {
        using var limiter = new LanPageRenderLimiter(maxConcurrentRenders: 1);

        Assert.True(limiter.TryAcquire(out IDisposable lease));
        lease.Dispose();

        Assert.True(limiter.TryAcquire(out IDisposable reacquired));
        reacquired.Dispose();
    }
}
