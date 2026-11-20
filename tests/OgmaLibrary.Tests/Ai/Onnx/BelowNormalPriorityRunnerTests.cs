using OgmaLibrary.Infrastructure.AI.Onnx;

namespace OgmaLibrary.Tests.Ai.Onnx;

/// <summary>Sept-23 Phase 14 slice 4: background inference runs below normal priority.</summary>
public sealed class BelowNormalPriorityRunnerTests
{
    [Fact]
    public async Task Work_RunsOnABelowNormalBackgroundThread()
    {
        (ThreadPriority priority, bool background, bool pooled) = await BelowNormalPriorityRunner.RunAsync(
            () => (Thread.CurrentThread.Priority, Thread.CurrentThread.IsBackground, Thread.CurrentThread.IsThreadPoolThread),
            CancellationToken.None);

        Assert.Equal(ThreadPriority.BelowNormal, priority);
        Assert.True(background);
        Assert.False(pooled);
    }

    [Fact]
    public async Task Failure_IsSurfacedToTheCaller()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BelowNormalPriorityRunner.RunAsync<int>(() => throw new InvalidOperationException("inference failed"), CancellationToken.None));
    }

    [Fact]
    public async Task CancelledBeforeStart_DoesNotRunTheWork()
    {
        bool ran = false;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BelowNormalPriorityRunner.RunAsync(() => ran = true, cancelled.Token));
        Assert.False(ran);
    }
}
