using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Tests.Pdf;

/// <summary>
/// Sept-23 Kaizen Phase 04 unit tests for the worker session protocol building blocks:
/// request-id routing (T04.3), priority scheduling (T04.4) and the DPI-aware render
/// width policy (T04.6).
/// </summary>
public sealed class PdfWorkerProtocolTests
{
    [Fact]
    public async Task ResponseRouter_DelayedResponseAfterTimeout_IsDiscardedAndNextRequestGetsItsOwn()
    {
        var router = new WorkerResponseRouter<string>();
        long slow = router.NextRequestId();
        Task<string> slowResponse = router.Register(slow);

        // The caller gives up on the slow request (timeout) and abandons it.
        await Assert.ThrowsAsync<TimeoutException>(() => slowResponse.WaitAsync(TimeSpan.FromMilliseconds(10)));
        router.Abandon(slow);

        long next = router.NextRequestId();
        Task<string> nextResponse = router.Register(next);

        // The worker's late reply to the slow request arrives first.
        Assert.False(router.TryComplete(slow, "late reply for slow request"));
        Assert.True(router.TryComplete(next, "reply for next request"));

        Assert.Equal("reply for next request", await nextResponse);
        Assert.Equal(1, router.DiscardedCount);
        Assert.Equal(0, router.PendingCount);
    }

    [Fact]
    public async Task ResponseRouter_OutOfOrderResponses_AreRoutedById()
    {
        var router = new WorkerResponseRouter<long>();
        long[] ids = [router.NextRequestId(), router.NextRequestId(), router.NextRequestId()];
        Task<long>[] responses = [.. ids.Select(router.Register)];

        foreach (long id in ids.Reverse())
        {
            Assert.True(router.TryComplete(id, id * 10));
        }

        long[] values = await Task.WhenAll(responses);
        Assert.Equal(ids.Select(id => id * 10), values);
    }

    [Fact]
    public async Task ResponseRouter_FailAll_FailsPendingAndLaterRegistrations()
    {
        var router = new WorkerResponseRouter<string>();
        Task<string> pending = router.Register(router.NextRequestId());

        router.FailAll(new PdfWorkerSessionLostException("worker_exited"));
        Task<string> late = router.Register(router.NextRequestId());

        PdfWorkerSessionLostException first = await Assert.ThrowsAsync<PdfWorkerSessionLostException>(() => pending);
        await Assert.ThrowsAsync<PdfWorkerSessionLostException>(() => late);
        Assert.Equal("worker_exited", first.Reason);
    }

    [Fact]
    public async Task PriorityGate_AdmitsControlThenPreviewThenRender()
    {
        using var gate = new PriorityRequestGate();
        await gate.WaitAsync(WorkerRequestPriority.Render, CancellationToken.None);
        var order = new List<string>();

        Task render1 = Admit(WorkerRequestPriority.Render, "render-1");
        Task render2 = Admit(WorkerRequestPriority.Render, "render-2");
        Task preview = Admit(WorkerRequestPriority.Preview, "preview");
        Task control = Admit(WorkerRequestPriority.Control, "control");
        Assert.Equal(4, gate.QueueDepth);

        gate.Release();
        await Task.WhenAll(render1, render2, preview, control);

        Assert.Equal(["control", "preview", "render-1", "render-2"], order);
        Assert.Equal(0, gate.QueueDepth);

        async Task Admit(WorkerRequestPriority priority, string name)
        {
            await gate.WaitAsync(priority, CancellationToken.None);
            lock (order)
            {
                order.Add(name);
            }

            gate.Release();
        }
    }

    [Fact]
    public async Task PriorityGate_CancelledWaiter_IsRemovedWithoutBlockingOthers()
    {
        using var gate = new PriorityRequestGate();
        await gate.WaitAsync(WorkerRequestPriority.Control, CancellationToken.None);
        using var stale = new CancellationTokenSource();

        Task stalePrefetch = gate.WaitAsync(WorkerRequestPriority.Render, stale.Token);
        Task visible = gate.WaitAsync(WorkerRequestPriority.Render, CancellationToken.None);
        await stale.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stalePrefetch);
        Assert.Equal(1, gate.QueueDepth);
        gate.Release();
        await visible.WaitAsync(TimeSpan.FromSeconds(5));
        gate.Release();
        Assert.Equal(0, gate.QueueDepth);
    }

    [Fact]
    public async Task PriorityGate_Dispose_FailsQueuedWaiters()
    {
        var gate = new PriorityRequestGate();
        await gate.WaitAsync(WorkerRequestPriority.Render, CancellationToken.None);
        Task queued = gate.WaitAsync(WorkerRequestPriority.Render, CancellationToken.None);

        gate.Dispose();
        gate.Release();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
    }

    [Theory]
    [InlineData(720.0, 1.0, 768)]
    [InlineData(720.0, 1.5, 1152)]
    [InlineData(720.0, 2.0, 1536)]
    [InlineData(1100.0, 1.25, 1408)]
    [InlineData(1100.0, 1.0, 1152)]
    public void ComputePageWidthPx_TracksDisplayWidthAndScaling_InBuckets(double displayWidthDip, double scaling, int expected)
    {
        int width = ReaderRenderDefaults.ComputePageWidthPx(displayWidthDip, scaling);

        Assert.Equal(expected, width);
        Assert.Equal(0, width % ReaderRenderDefaults.WidthBucketPx);
        Assert.True(width >= Math.Ceiling(displayWidthDip * scaling), "The raster must not be narrower than the device pixels.");
    }

    [Fact]
    public void ComputePageWidthPx_IsBoundedByWidthCapAndPixelBudget()
    {
        Assert.Equal(ReaderRenderDefaults.MaxPageWidthPx, ReaderRenderDefaults.ComputePageWidthPx(4000, 2.0, heightToWidthRatio: 0.5));

        int tall = ReaderRenderDefaults.ComputePageWidthPx(4000, 2.0, heightToWidthRatio: 1.414);
        Assert.True((long)tall * (long)(tall * 1.414) <= ReaderRenderDefaults.MaxPagePixels);

        Assert.Equal(ReaderRenderDefaults.PageWidthPx, ReaderRenderDefaults.ComputePageWidthPx(0, 1.0));
        Assert.Equal(ReaderRenderDefaults.MinPageWidthPx, ReaderRenderDefaults.ComputePageWidthPx(20, 1.0));
        Assert.Equal(
            ReaderRenderDefaults.ComputePageWidthPx(720, 1.0),
            ReaderRenderDefaults.ComputePageWidthPx(720, double.NaN));
    }
}
