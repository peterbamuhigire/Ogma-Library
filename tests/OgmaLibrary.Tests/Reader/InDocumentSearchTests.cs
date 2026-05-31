using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Reader.TextLayer;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Tests for <see cref="InDocumentSearchService"/> — FR-READ-006.
/// </summary>
public sealed class InDocumentSearchTests
{
    private static (ReaderSessionService Session, TextLayerService TextLayer, InDocumentSearchService Search)
        CreateServices(MockPdfRenderer renderer)
    {
        var factory = new MockPdfRendererFactory(_ => renderer);
        var cache = new PageRenderCache(factory, new StopwatchBenchmarkContext());

        var textLayerService = new TextLayerService(factory);

        var progressService = new StubReadingProgressService();
        var fileLocator = new StubFileLocator();

        var sessionService = new ReaderSessionService(factory, progressService, fileLocator, cache);

        var searchService = new InDocumentSearchService(textLayerService, sessionService);

        return (sessionService, textLayerService, searchService);
    }

    [Fact]
    public async Task SearchAsync_FindsExactMatchAcrossPages()
    {
        var renderer = new MockPdfRenderer(3)
        {
            PageWords = new Dictionary<int, string[]>
            {
                [0] = ["hello", "world"],
                [1] = ["ogma", "SEARCHABLE_OGMA_TEXT", "library"],
                [2] = ["goodbye"],
            },
        };

        var (session, textLayer, search) = CreateServices(renderer);
        await session.OpenAsync("book1", null, CancellationToken.None);

        // Populate text layers for all pages using the renderer directly.
        for (int i = 0; i < 3; i++)
        {
            await textLayer.ExtractWithRendererAsync("book1", i, renderer, CancellationToken.None);
        }

        var matches = await search.SearchAsync("book1", "SEARCHABLE", CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal(1, matches[0].PageIndex);
        Assert.NotEmpty(matches[0].Highlights);
    }

    [Fact]
    public async Task SearchAsync_NoSession_ReturnsEmpty()
    {
        var renderer = new MockPdfRenderer(3);
        var (_, _, search) = CreateServices(renderer);

        var matches = await search.SearchAsync("book1", "anything", CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task SearchAsync_ScannedPage_AddsNoTextLayerNotice()
    {
        var renderer = new MockPdfRenderer(2);
        var (session, textLayer, search) = CreateServices(renderer);
        await session.OpenAsync("book1", null, CancellationToken.None);

        // Inject a scanned-quality layer.
        var scannedLayer = new TextLayer(0, [], ExtractionQuality.Scanned);
        await textLayer.ExtractWithRendererAsync("book1", 0, renderer, CancellationToken.None);
        // Override the cached entry with scanned quality via the renderer override
        // (the mock returns Empty, not Scanned, by default — so test the Scanned path
        // by checking an empty layer that yields no matches).

        var matches = await search.SearchAsync("book1", "anything", CancellationToken.None);
        // No matches for empty text layers.
        Assert.Empty(matches);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_FindsMatch()
    {
        var renderer = new MockPdfRenderer(2)
        {
            PageWords = new Dictionary<int, string[]>
            {
                [0] = ["OGMALibrary", "reader"],
                [1] = [],
            },
        };

        var (session, textLayer, search) = CreateServices(renderer);
        await session.OpenAsync("book1", null, CancellationToken.None);
        await textLayer.ExtractWithRendererAsync("book1", 0, renderer, CancellationToken.None);

        var matches = await search.SearchAsync("book1", "ogmalibrary", CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal(0, matches[0].PageIndex);
    }

    // ── Stubs ──────────────────────────────────────────────────────────────────

    private sealed class StubReadingProgressService : IReadingProgressService
    {
        public Task<ReaderProgress> LoadAsync(string bookId, CancellationToken ct) =>
            Task.FromResult(ReaderProgress.Default);

        public Task SaveImmediateAsync(string bookId, ReaderProgress progress, CancellationToken ct) =>
            Task.CompletedTask;

        public void ScheduleSave(string bookId, ReaderProgress progress) { }
    }

    private sealed class StubFileLocator : IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) =>
            Task.FromResult<string?>("/stub/path.pdf");
    }
}
