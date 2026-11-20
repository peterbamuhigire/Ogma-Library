using System.Diagnostics;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Ai.Onnx;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// The synthetic corpus catalogue embedded with the built-in ONNX provider and searched
/// through the unified (RRF) pipeline with semantic results on. Sept-23 Phase 14, slice 6
/// (tasks 14.9 and 14.10). Needs <c>OGMA_TEST_EMBEDDING_MODEL_DIR</c>; callers skip visibly
/// (NOT_ASSESSED) when it is unset.
/// </summary>
internal sealed class SemanticCorpusIndex : IDisposable
{
    private const int BatchSize = 64;

    private SemanticCorpusIndex(SyntheticCorpusCatalogue corpus, OnnxEmbeddingProvider provider)
    {
        Corpus = corpus;
        Provider = provider;
        var fullText = new FtsIndexService(corpus.Context);
        var semantic = new SemanticSearchService(
            corpus.Context,
            provider,
            new CombinedSearchService(new MetadataSearchService(corpus.Context), fullText),
            capability: new FakeSemanticCapability(available: true));
        Search = new UnifiedSearchService(
            corpus.Context,
            fullText,
            semantic,
            new FakeSemanticCapability(available: true));
    }

    /// <summary>The seeded catalogue.</summary>
    public SyntheticCorpusCatalogue Corpus { get; }

    /// <summary>The built-in provider over the configured model.</summary>
    public OnnxEmbeddingProvider Provider { get; }

    /// <summary>The unified search pipeline with semantic results on.</summary>
    public UnifiedSearchService Search { get; }

    /// <summary>Chunks embedded while building the index.</summary>
    public int ChunksEmbedded { get; private set; }

    /// <summary>Chunks that failed while building the index.</summary>
    public int ChunksFailed { get; private set; }

    /// <summary>Wall time of the index build.</summary>
    public TimeSpan BuildTime { get; private set; }

    /// <summary>Creates the corpus and embeds every chunk, or skips when no model is configured.</summary>
    public static async Task<SemanticCorpusIndex> CreateOrSkipAsync()
    {
        OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();
        var index = new SemanticCorpusIndex(SyntheticCorpusCatalogue.Create(), provider);
        await index.BuildAsync().ConfigureAwait(false);
        return index;
    }

    /// <summary>Runs one query and returns the display titles, best first.</summary>
    public async Task<IReadOnlyList<UnifiedSearchResult>> SearchAsync(string query) =>
        (await Search.SearchAsync(query, 30, CancellationToken.None).ConfigureAwait(false)).Results;

    /// <inheritdoc />
    public void Dispose()
    {
        Provider.Dispose();
        Corpus.Dispose();
    }

    private async Task BuildAsync()
    {
        var generation = new EmbeddingGenerationService(
            Corpus.Context,
            Provider,
            new EmbeddingVectorRepository(Corpus.Context));
        var clock = Stopwatch.StartNew();
        while (true)
        {
            EmbeddingGenerationBatchResult batch = await generation
                .GenerateNextBatchAsync(BatchSize, CancellationToken.None)
                .ConfigureAwait(false);
            if (batch.ProviderUnavailable)
            {
                throw new InvalidOperationException("The configured embedding model did not verify.");
            }

            ChunksEmbedded += batch.ChunksEmbedded;
            ChunksFailed += batch.ChunksFailed;
            if (batch.ChunksAttempted == 0 || batch.ChunksEmbedded == 0)
            {
                break;
            }
        }

        BuildTime = clock.Elapsed;
        Corpus.Context.ChangeTracker.Clear();
    }
}
