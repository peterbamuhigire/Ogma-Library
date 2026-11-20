using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Contract every <see cref="IEmbeddingProvider"/> must meet. Concrete
/// providers (fake, ONNX, Ollama) derive from this class and supply an
/// available instance. The tests are skippable so a provider whose model is
/// not installed on this machine reports a visible skip from
/// <see cref="CreateProvider"/> instead of a pass or a failure.
/// </summary>
public abstract class EmbeddingProviderContractTests
{
    private protected abstract IEmbeddingProvider CreateProvider();

    [SkippableFact]
    public async Task Contract_VectorDimension_EqualsDeclaredDimensions()
    {
        IEmbeddingProvider provider = CreateProvider();

        EmbeddingResult passage = await provider.EmbedAsync(
            "Light splits into colours through a prism.",
            EmbeddingInputKind.Passage,
            CancellationToken.None);
        EmbeddingResult query = await provider.EmbedAsync(
            "books about light and colour",
            EmbeddingInputKind.Query,
            CancellationToken.None);

        Assert.True(provider.Dimensions > 0);
        Assert.Equal(provider.Dimensions, passage.Vector.Length);
        Assert.Equal(provider.Dimensions, query.Vector.Length);
    }

    [SkippableTheory]
    [InlineData("Light splits into colours through a prism.")]
    [InlineData("Les royaumes des Grands Lacs avant la colonisation.")]
    [InlineData("Wanjirũ read the chapter on rainforest animals.")]
    public async Task Contract_Vector_IsFiniteAndUnitNorm(string text)
    {
        IEmbeddingProvider provider = CreateProvider();

        EmbeddingResult result = await provider.EmbedAsync(
            text,
            EmbeddingInputKind.Passage,
            CancellationToken.None);

        Assert.All(result.Vector, value => Assert.True(float.IsFinite(value)));
        double norm = Math.Sqrt(result.Vector.Sum(value => (double)value * value));
        Assert.InRange(norm, 0.999, 1.001);
    }

    [SkippableFact]
    public async Task Contract_CancelledToken_IsHonoured()
    {
        IEmbeddingProvider provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.EmbedAsync(
            "cancelled before start",
            EmbeddingInputKind.Query,
            cancellation.Token));
    }

    [SkippableFact]
    public async Task Contract_Provenance_IsNonEmptyAndMatchesProvider()
    {
        IEmbeddingProvider provider = CreateProvider();

        EmbeddingResult result = await provider.EmbedAsync(
            "provenance",
            EmbeddingInputKind.Passage,
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(provider.ProviderKey));
        Assert.False(string.IsNullOrWhiteSpace(provider.ModelId));
        Assert.False(string.IsNullOrWhiteSpace(provider.ModelVersion));
        Assert.Equal(provider.ProviderKey, result.ProviderKey);
        Assert.Equal(provider.ModelId, result.ModelId);
        Assert.Equal(provider.ModelVersion, result.ModelVersion);
    }

    [SkippableFact]
    public async Task Contract_AvailableProvider_ReportsAvailable()
    {
        IEmbeddingProvider provider = CreateProvider();

        Assert.True(await provider.IsAvailableAsync(CancellationToken.None));
    }
}

/// <summary>Runs the provider contract against the in-memory hashing fake.</summary>
public sealed class HashingEmbeddingProviderContractTests : EmbeddingProviderContractTests
{
    private protected override IEmbeddingProvider CreateProvider() => new HashingEmbeddingProvider(16);
}

/// <summary>
/// Runs the provider contract against the Ollama adapter with a stubbed
/// loopback service (no real Ollama process is contacted).
/// </summary>
public sealed class OllamaEmbeddingAdapterContractTests : EmbeddingProviderContractTests, IDisposable
{
    private readonly List<HttpClient> _clients = [];

    public void Dispose()
    {
        foreach (HttpClient client in _clients)
        {
            client.Dispose();
        }
    }

    private protected override IEmbeddingProvider CreateProvider()
    {
        var http = new HttpClient(new Phase11EmbeddingSchemaTests.RecordingHandler(request =>
            request.RequestUri?.AbsolutePath == "/api/tags"
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                : Phase11EmbeddingSchemaTests.OllamaVectorResponse(
                    Enumerable.Range(1, 768).Select(i => (float)Math.Sin(i)).ToArray())))
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };
        _clients.Add(http);
        return new OgmaLibrary.Infrastructure.AI.Ollama.OllamaEmbeddingAdapter(http);
    }
}
