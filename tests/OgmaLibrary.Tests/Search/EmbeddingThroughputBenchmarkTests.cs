using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Search;
using OgmaLibrary.Tests.Ai.Onnx;
using OgmaLibrary.Tests.Catalogue;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Sept-23 Phase 14, task 14.10 (slice 6): embedding throughput and peak working set for the
/// synthetic corpus and a 2,000-book synthetic catalogue, and keyword latency with the built-in
/// provider absent against the semantic-off baseline (task 14.6, target within ±10 %).
/// Tagged Performance and Benchmark so the fast suite never runs it. The throughput case needs
/// <c>OGMA_TEST_EMBEDDING_MODEL_DIR</c> and skips visibly (NOT_ASSESSED) without it. Results are
/// written as JSON to the Phase 14 evidence folder.
/// </summary>
public sealed class EmbeddingThroughputBenchmarkTests
{
    private const int SyntheticBooks = 2_000;
    private const int LatencyRounds = 12;
    private const double LatencyTolerance = 0.10;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string[] KeywordQueries =
    [
        "river", "Lantern Keeper 0042", "caravan monsoon", "author:Author 0500", "algoritm",
        "\"harvest and the\"", "kingdom -river", "Book 1999", "isbn:9780000001234", "zzqxnotaword",
        "trade archive", "savana", "compass orbit prism", "school", "Delta Harbour",
    ];

    private readonly ITestOutputHelper _output;

    public EmbeddingThroughputBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Category", "Benchmark")]
    public async Task KeywordLatency_ProviderAbsent_WithinTenPercentOfSemanticOff()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        string dataDirectory = Directory.CreateTempSubdirectory("ogma-bench-absent-").FullName;
        var ollama = new UnreachableOllamaProvider();
        try
        {
            context.Database.Migrate();
            UnifiedSearchPerformanceTests.Seed(context, SyntheticBooks, 10);
            var store = new EmbeddingModelStore(dataDirectory, EmbeddingModelManifest.LoadDefault());
            using var builtIn = new OnnxEmbeddingProvider(store);
            using var registry = new EmbeddingProviderRegistry(builtIn, ollama, store: store, environmentOverride: () => null);
            Assert.False(await registry.RefreshAsync(CancellationToken.None));

            var fullText = new FtsIndexService(context);
            var semanticOff = new UnifiedSearchService(context, fullText);
            var providerAbsent = new UnifiedSearchService(
                context,
                fullText,
                new SemanticSearchService(
                    context,
                    registry,
                    new CombinedSearchService(new MetadataSearchService(context), fullText),
                    capability: registry),
                registry);

            foreach (string query in KeywordQueries)
            {
                _ = await semanticOff.SearchAsync(query, 30, CancellationToken.None);
                _ = await providerAbsent.SearchAsync(query, 30, CancellationToken.None);
            }

            // Interleaved, alternating order per round, so drift and caching affect both equally.
            var off = new List<double>();
            var absent = new List<double>();
            for (int round = 0; round < LatencyRounds; round++)
            {
                foreach (string query in KeywordQueries)
                {
                    if (round % 2 == 0)
                    {
                        off.Add(await TimeAsync(semanticOff, query));
                        absent.Add(await TimeAsync(providerAbsent, query));
                    }
                    else
                    {
                        absent.Add(await TimeAsync(providerAbsent, query));
                        off.Add(await TimeAsync(semanticOff, query));
                    }
                }
            }

            double offTotal = off.Sum();
            double absentTotal = absent.Sum();
            double ratio = absentTotal / offTotal;
            var report = new
            {
                benchmark = "keyword-latency-provider-absent",
                phase = "sept-23-kaizen/phase-14/slice-6",
                measuredUtc = DateTimeOffset.UtcNow,
                machine = Machine(),
                corpus = new { books = SyntheticBooks, pagesPerBook = 10, generator = "UnifiedSearchPerformanceTests.Seed (seed 13)" },
                method = "Release build; one warm-up pass; " + LatencyRounds + " interleaved rounds of " + KeywordQueries.Length +
                    " keyword queries, order alternating per round. Semantic off = no semantic service; provider absent = " +
                    "the real EmbeddingProviderRegistry over an OnnxEmbeddingProvider whose model is not installed.",
                semanticOff = Summary(off),
                providerAbsent = Summary(absent),
                ratioOfTotals = Math.Round(ratio, 4),
                target = "ratio within 1 ± " + LatencyTolerance.ToString(CultureInfo.InvariantCulture),
                ollamaProbes = ollama.Probes,
                ollamaEmbeds = ollama.Embeds,
            };
            WriteEvidence("keyword-latency-provider-absent.json", report);
            _output.WriteLine($"semantic off {offTotal:F1} ms, provider absent {absentTotal:F1} ms, ratio {ratio:F3}");

            Assert.Equal(0, ollama.Probes + ollama.Embeds);
            Assert.True(
                Math.Abs(ratio - 1) <= LatencyTolerance,
                $"provider-absent keyword latency is {ratio:F3}× the semantic-off baseline (target 1 ± {LatencyTolerance})");
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [SkippableFact]
    [Trait("Category", "Performance")]
    [Trait("Category", "Benchmark")]
    public async Task EmbeddingThroughput_CorpusAndSynthetic2000_Recorded()
    {
        EmbeddingModelTestFolder.RequireOrSkip();
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        long workingSetBefore = process.WorkingSet64;

        object corpusRun;
        List<object> conceptualRanks = [];
        using (SemanticCorpusIndex index = await SemanticCorpusIndex.CreateOrSkipAsync())
        {
            process.Refresh();
            corpusRun = Run("synthetic corpus catalogue", SyntheticCorpusCatalogue.WorkCount, index.ChunksEmbedded, index.ChunksFailed, index.BuildTime, process.PeakWorkingSet64);
            Assert.Equal(0, index.ChunksFailed);
            foreach (object[] row in SearchRelevanceOracleTests.ConceptualTopThreeCases)
            {
                string query = (string)row[0];
                string expected = (string)row[1];
                var clock = Stopwatch.StartNew();
                IReadOnlyList<UnifiedSearchResult> results = await index.SearchAsync(query);
                double ms = clock.Elapsed.TotalMilliseconds;
                int rank = results.Select(result => result.Title).ToList().IndexOf(expected);
                conceptualRanks.Add(new { query, expected, rank = rank < 0 ? (int?)null : rank + 1, queryMs = Math.Round(ms, 1) });
            }
        }

        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        object syntheticRun;
        try
        {
            context.Database.Migrate();
            UnifiedSearchPerformanceTests.Seed(context, SyntheticBooks, 1);
            using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();
            var generation = new EmbeddingGenerationService(context, provider, new EmbeddingVectorRepository(context));
            int embedded = 0;
            int failed = 0;
            var clock = Stopwatch.StartNew();
            while (true)
            {
                EmbeddingGenerationBatchResult batch = await generation.GenerateNextBatchAsync(64, CancellationToken.None);
                embedded += batch.ChunksEmbedded;
                failed += batch.ChunksFailed;
                if (batch.ProviderUnavailable || batch.ChunksAttempted == 0 || batch.ChunksEmbedded == 0)
                {
                    break;
                }
            }

            clock.Stop();
            process.Refresh();
            syntheticRun = Run("2,000-book synthetic catalogue, one 60-word page per book", SyntheticBooks, embedded, failed, clock.Elapsed, process.PeakWorkingSet64);
            Assert.Equal(0, failed);
            Assert.Equal(SyntheticBooks, embedded);
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }

        var report = new
        {
            benchmark = "embedding-throughput",
            phase = "sept-23-kaizen/phase-14/slice-6",
            measuredUtc = DateTimeOffset.UtcNow,
            machine = Machine(),
            model = new { EmbeddingModelManifest.LoadDefault().ModelId, EmbeddingModelManifest.LoadDefault().Revision },
            method = "Release build; EmbeddingGenerationService batches of 64 with the built-in ONNX provider " +
                "(one input at a time, passage inference on a below-normal-priority thread). Peak working set is the " +
                "test host's cumulative process peak, so the second run's value includes the first.",
            workingSetBeforeMb = Mb(workingSetBefore),
            runs = new[] { corpusRun, syntheticRun },
            conceptualRanks,
        };
        WriteEvidence("embedding-throughput.json", report);
    }

    private object Run(string name, int books, int embedded, int failed, TimeSpan elapsed, long peakWorkingSet)
    {
        double rate = embedded / Math.Max(elapsed.TotalSeconds, 0.001);
        _output.WriteLine($"{name}: {embedded} chunks in {elapsed.TotalSeconds:F1} s = {rate:F2} chunks/s, peak {Mb(peakWorkingSet)} MB");
        return new
        {
            name,
            books,
            chunksEmbedded = embedded,
            chunksFailed = failed,
            seconds = Math.Round(elapsed.TotalSeconds, 2),
            chunksPerSecond = Math.Round(rate, 2),
            peakWorkingSetMb = Mb(peakWorkingSet),
        };
    }

    private static async Task<double> TimeAsync(UnifiedSearchService service, string query)
    {
        var clock = Stopwatch.StartNew();
        _ = await service.SearchAsync(query, 30, CancellationToken.None);
        return clock.Elapsed.TotalMilliseconds;
    }

    private static object Summary(List<double> samples)
    {
        List<double> sorted = [.. samples.Order()];
        return new
        {
            n = sorted.Count,
            totalMs = Math.Round(sorted.Sum(), 1),
            p50Ms = Math.Round(sorted[(sorted.Count / 2) - 1], 2),
            p95Ms = Math.Round(sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1], 2),
            maxMs = Math.Round(sorted[^1], 2),
        };
    }

    private static object Machine() => new
    {
        os = RuntimeInformation.OSDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        logicalProcessors = Environment.ProcessorCount,
        processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
        runtime = RuntimeInformation.FrameworkDescription,
    };

    private static long Mb(long bytes) => bytes / (1024 * 1024);

    private static void WriteEvidence(string fileName, object report)
    {
        string folder = Path.Combine(
            RepositoryTestPaths.Root,
            "docs", "implementation", "execution", "evidence", "sept-23-kaizen", "phase-14");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, fileName), JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine);
    }

    /// <summary>Advanced provider stand-in that records any call: none may happen while Built-in is chosen.</summary>
    private sealed class UnreachableOllamaProvider : IOllamaEmbeddingProvider
    {
        public int Probes { get; private set; }

        public int Embeds { get; private set; }

        public string ProviderKey => "ollama";

        public bool IsLocalOnly => true;

        public string ModelId => "nomic-embed-text";

        public string ModelVersion => "test";

        public int Dimensions => 768;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            Probes++;
            return Task.FromResult(false);
        }

        public Task<EmbeddingResult> EmbedAsync(string text, EmbeddingInputKind kind, CancellationToken cancellationToken)
        {
            Embeds++;
            throw new InvalidOperationException("The advanced provider is not reachable in this benchmark.");
        }
    }
}
