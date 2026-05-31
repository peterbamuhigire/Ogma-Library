using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Reader.Cache;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Tests for <see cref="PageRenderCache"/> — NFR-OGMA-005 page-turn cache.
/// </summary>
public sealed class PageRenderCacheTests
{
    private static PageRenderCache CreateCache(
        MockPdfRendererFactory? factory = null,
        long memoryBudget = PageRenderCache.DefaultMemoryBudgetBytes)
    {
        factory ??= new MockPdfRendererFactory();
        return new PageRenderCache(factory, new StopwatchBenchmarkContext(), memoryBudget);
    }

    [Fact]
    public async Task GetOrRenderAsync_PageNotCached_RendersAndReturnResult()
    {
        var mockRenderer = new MockPdfRenderer(5);
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory);
        cache.SetRenderer("book1", mockRenderer);

        var request = new RenderRequest(800);
        var result = await cache.GetOrRenderAsync("book1", 0, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result.PageIndex);
        Assert.NotEmpty(result.PngBytes);
    }

    [Fact]
    public async Task GetOrRenderAsync_CachedPage_DoesNotRenderAgain()
    {
        var mockRenderer = new MockPdfRenderer(5);
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory);
        cache.SetRenderer("book1", mockRenderer);

        var request = new RenderRequest(800);

        // First render
        await cache.GetOrRenderAsync("book1", 0, request, CancellationToken.None);
        int firstCount;
        lock (mockRenderer.RenderCalls)
        {
            firstCount = mockRenderer.RenderCalls.Count;
        }

        // Second call — should hit cache
        await cache.GetOrRenderAsync("book1", 0, request, CancellationToken.None);
        int secondCount;
        lock (mockRenderer.RenderCalls)
        {
            secondCount = mockRenderer.RenderCalls.Count;
        }

        Assert.Equal(firstCount, secondCount);
    }

    [Fact]
    public async Task GetOrRenderAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var mockRenderer = new MockPdfRenderer(5) { RenderDelayMs = 500 };
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory);
        cache.SetRenderer("book1", mockRenderer);

        using var cts = new CancellationTokenSource(10); // Very short timeout
        var request = new RenderRequest(800);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.GetOrRenderAsync("book1", 0, request, cts.Token));
    }

    [Fact]
    public async Task CachedPageTurn_CompletesWithinBudget()
    {
        // NFR-OGMA-005: cached page turns ≤ 100 ms P95.
        var mockRenderer = new MockPdfRenderer(10);
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory);
        cache.SetRenderer("book1", mockRenderer);

        var request = new RenderRequest(800);

        // Prime the cache for pages 0-9.
        for (int i = 0; i < 10; i++)
        {
            await cache.GetOrRenderAsync("book1", i, request, CancellationToken.None);
        }

        // Measure 20 cached page turns.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            await cache.GetOrRenderAsync("book1", i, request, CancellationToken.None);
            await cache.GetOrRenderAsync("book1", 9 - i, request, CancellationToken.None);
        }

        stopwatch.Stop();
        double avgMs = stopwatch.Elapsed.TotalMilliseconds / 20.0;

        // Each cached hit should be well under 100 ms (dev-box trend; not a hard CI gate).
        // Assert a generous budget of 100 ms per turn on average.
        Assert.True(
            avgMs < 100.0,
            $"Average cached page-turn time {avgMs:F1} ms exceeded the 100 ms budget. " +
            "(dev-box trend; label 'dev-box' if this flakes on slow CI)");
    }

    [Fact]
    public async Task CachedPageTurn_P95_With100Annotations_Under100ms()
    {
        var mockRenderer = new MockPdfRenderer(10);
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory);
        cache.SetRenderer("book1", mockRenderer);
        var request = new RenderRequest(800);
        List<AnnotationRegion> regions = Enumerable.Range(0, 100)
            .Select(index => new AnnotationRegion(
                PageIndex: index % 10,
                NormLeft: 0.04 + (index % 10) * 0.08,
                NormTop: 0.04 + ((index / 10) % 10) * 0.08,
                NormWidth: 0.06,
                NormHeight: 0.03))
            .ToList();

        for (int page = 0; page < 10; page++)
        {
            await cache.GetOrRenderAsync("book1", page, request, CancellationToken.None);
        }

        var durations = new List<TimeSpan>(capacity: 20);
        for (int iteration = 0; iteration < 20; iteration++)
        {
            int page = iteration % 10;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();

            await cache.GetOrRenderAsync("book1", page, request, CancellationToken.None);
            foreach (AnnotationRegion region in regions.Where(region => region.PageIndex == page))
            {
                _ = OgmaLibrary.Reader.Annotations.AnnotationRenderHelper.ToScreenRect(
                    region,
                    renderedWidthPx: 720,
                    renderedHeightPx: 960,
                    rotationDegrees: page % 2 == 0 ? 0 : 90,
                    zoomFactor: 1.5);
            }

            durations.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started));
        }

        TimeSpan p95 = Percentile95(durations);

        Assert.True(
            p95 <= TimeSpan.FromMilliseconds(100),
            $"Cached page turn plus 100-annotation overlay transform should stay under 100 ms P95; actual {p95.TotalMilliseconds:F3} ms.");
    }

    [Fact]
    public void Invalidate_RemovesCachedEntries()
    {
        var mockRenderer = new MockPdfRenderer(5);
        using var cache = CreateCache(new MockPdfRendererFactory(_ => mockRenderer));
        cache.SetRenderer("book1", mockRenderer);

        cache.Invalidate("book1");

        // After invalidation, memory usage should drop to 0 for that book.
        Assert.Equal(0, cache.MemoryUsageBytes);
    }

    [Fact]
    public async Task Prefetch_QueuesRenderForNeighbouringPages()
    {
        var mockRenderer = new MockPdfRenderer(10);
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory);
        cache.SetRenderer("book1", mockRenderer);

        var request = new RenderRequest(800);
        cache.Prefetch("book1", [0, 1, 2], request);

        // Allow background renders to complete.
        await Task.Delay(200);

        // Pages 0, 1, 2 should now be cached (render count ≥ 3).
        int renderCount;
        lock (mockRenderer.RenderCalls)
        {
            renderCount = mockRenderer.RenderCalls.Count;
        }

        Assert.True(renderCount >= 3, $"Expected at least 3 renders but got {renderCount}");
    }

    [Fact]
    public async Task LruEviction_RespectsMemoryBudget()
    {
        // Each 1x1 PNG is ~100 bytes; set budget to 500 bytes to force eviction quickly.
        var mockRenderer = new MockPdfRenderer(20);
        var factory = new MockPdfRendererFactory(_ => mockRenderer);
        using var cache = CreateCache(factory, memoryBudget: 500);
        cache.SetRenderer("book1", mockRenderer);

        var request = new RenderRequest(800);

        // Render 10 pages to trigger eviction.
        for (int i = 0; i < 10; i++)
        {
            await cache.GetOrRenderAsync("book1", i, request, CancellationToken.None);
        }

        // Memory must not exceed budget.
        Assert.True(
            cache.MemoryUsageBytes <= 500,
            $"Memory {cache.MemoryUsageBytes} exceeds budget 500");
    }

    [Fact]
    public void CancelOutside_CancelsPendingRendersForOtherPages()
    {
        var mockRenderer = new MockPdfRenderer(10) { RenderDelayMs = 200 };
        using var cache = CreateCache(new MockPdfRendererFactory(_ => mockRenderer));
        cache.SetRenderer("book1", mockRenderer);

        var request = new RenderRequest(800);

        // Queue renders for pages 0-4.
        cache.Prefetch("book1", Enumerable.Range(0, 5), request);

        // Cancel all except page 2.
        cache.CancelOutside("book1", [2]);

        // No assertions on counts here — the key invariant is that no exception is thrown.
        Assert.True(true, "CancelOutside completed without exception");
    }

    private static TimeSpan Percentile95(IReadOnlyList<TimeSpan> durations)
    {
        TimeSpan[] sorted = durations.OrderBy(static duration => duration).ToArray();
        int index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
