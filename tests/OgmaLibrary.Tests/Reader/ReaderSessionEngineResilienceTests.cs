using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Session;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Sept-23 Kaizen Phase 04: reader-session behaviour when the PDF engine fails, when
/// opens race, and when the render width changes (K30, K32, K33; T04.3, T04.6, T04.7).
/// </summary>
public sealed class ReaderSessionEngineResilienceTests : IDisposable
{
    private const string BookId = "resilience-book";
    private readonly MockPdfRendererFactory _factory = new(pageCount: 20);
    private readonly PageRenderCache _cache;
    private readonly ReaderSessionService _service;

    public ReaderSessionEngineResilienceTests()
    {
        _cache = new PageRenderCache(_factory, new StopwatchBenchmarkContext());
        _service = new ReaderSessionService(_factory, new NullProgress(), new Locator(), _cache);
    }

    [Theory]
    [InlineData(typeof(PdfRendererUnavailableException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task NavigateToAsync_EngineFailure_DoesNotThrowAndStillTurnsThePage(Type failureType)
    {
        await _service.OpenAsync(BookId, 3, CancellationToken.None);
        MockPdfRenderer renderer = _factory.CreatedRenderers.Single();
        renderer.GeometryFailure = (Exception)Activator.CreateInstance(failureType)!;

        Exception? thrown = await Record.ExceptionAsync(() => _service.NavigateToAsync(4));

        Assert.Null(thrown);
        Assert.Equal(4, _service.CurrentSession!.CurrentPageIndex);
    }

    [Fact]
    public async Task OpenAsync_RacingOpens_LeaveExactlyOneLiveRenderer()
    {
        Task<ReaderSession>[] opens = [.. Enumerable.Range(0, 8).Select(page => _service.OpenAsync(BookId, page, CancellationToken.None))];
        await Task.WhenAll(opens);

        Assert.Equal(8, _factory.CreatedRenderers.Count);
        MockPdfRenderer live = Assert.Single(_factory.CreatedRenderers, renderer => !renderer.IsDisposed);
        Assert.Same(live, _service.CurrentRenderer);
    }

    [Fact]
    public async Task CloseAsync_DisposesRendererOffTheCallingThread()
    {
        await _service.OpenAsync(BookId, 0, CancellationToken.None);
        MockPdfRenderer renderer = _factory.CreatedRenderers.Single();

        await _service.CloseAsync(CancellationToken.None);

        Assert.True(renderer.IsDisposed);
        Assert.Null(_service.CurrentRenderer);
        Assert.Null(_service.CurrentSession);
    }

    [Fact]
    public async Task UpdateRenderWidth_PrefetchesNeighboursAtTheReaderWidth()
    {
        await _service.OpenAsync(BookId, 5, CancellationToken.None);
        MockPdfRenderer renderer = _factory.CreatedRenderers.Single();

        _service.UpdateRenderWidth(1536);
        await _service.NavigateToAsync(6);
        await WaitForAsync(() => RenderedWidths(renderer).Contains(1536));

        Assert.Equal(1536, _service.RenderWidthPx);
        Assert.Contains(1536, RenderedWidths(renderer));
    }

    [Fact]
    public async Task EngineRecovery_IsPublishedAsReaderEvent()
    {
        var factory = new RecoveringFactory();
        using var cache = new PageRenderCache(factory, new StopwatchBenchmarkContext());
        using var service = new ReaderSessionService(factory, new NullProgress(), new Locator(), cache);
        var recovered = new List<ReaderEvent>();
        using IDisposable serviceSubscription = service.Events.Subscribe(new Observer(recovered));

        await service.OpenAsync(BookId, 0, CancellationToken.None);
        factory.Renderer.RaiseRecovered("worker_exited");

        ReaderEvent.EngineRecovered evt = Assert.IsType<ReaderEvent.EngineRecovered>(
            recovered.Single(e => e is ReaderEvent.EngineRecovered));
        Assert.Equal(BookId, evt.BookId);
        Assert.Equal("worker_exited", evt.Reason);
        Assert.Equal(1, evt.RespawnCount);
    }

    public void Dispose()
    {
        _service.Dispose();
        _cache.Dispose();
    }

    private static List<int> RenderedWidths(MockPdfRenderer renderer)
    {
        lock (renderer.RenderCalls)
        {
            return [.. renderer.RenderCalls.Select(call => call.Request.WidthPx)];
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class NullProgress : IReadingProgressService
    {
        public Task<ReaderProgress> LoadAsync(string bookId, CancellationToken ct) => Task.FromResult(ReaderProgress.Default);

        public Task SaveImmediateAsync(string bookId, ReaderProgress progress, CancellationToken ct) => Task.CompletedTask;

        public void ScheduleSave(string bookId, ReaderProgress progress)
        {
        }
    }

    private sealed class Locator : IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) => Task.FromResult<string?>("/test/book.pdf");
    }

    private sealed class Observer(List<ReaderEvent> sink) : IObserver<ReaderEvent>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(ReaderEvent value)
        {
            lock (sink)
            {
                sink.Add(value);
            }
        }
    }

    private sealed class RecoveringFactory : IPdfRendererFactory
    {
        public HealthRenderer Renderer { get; } = new();

        public IPdfRenderer Open(string filePath) => Renderer;
    }

    private sealed class HealthRenderer : IPdfRenderer, IPdfRendererHealth
    {
        private readonly MockPdfRenderer _inner = new(5);

        public event EventHandler<PdfRendererRecoveredEventArgs>? Recovered;

        public int PageCount => _inner.PageCount;

        public void RaiseRecovered(string reason) =>
            Recovered?.Invoke(this, new PdfRendererRecoveredEventArgs(reason, 1, TimeSpan.FromMilliseconds(120)));

        public PdfRendererHealthSnapshot GetHealthSnapshot() => new("Healthy", 1, 0, 0, 0, 0);

        public Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct) =>
            _inner.RenderPageAsync(pageIndex, request, ct);

        public int GetPageRotationDegrees(int pageIndex) => _inner.GetPageRotationDegrees(pageIndex);

        public TextLayer ExtractTextLayer(int pageIndex) => _inner.ExtractTextLayer(pageIndex);

        public void Dispose() => _inner.Dispose();
    }
}
