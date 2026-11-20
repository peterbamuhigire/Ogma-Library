using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;
using OgmaLibrary.Tests.Search;

namespace OgmaLibrary.Tests.Ai.Onnx;

/// <summary>
/// Runs the provider contract against the built-in ONNX provider. Skipped
/// visibly when <c>OGMA_TEST_EMBEDDING_MODEL_DIR</c> is unset.
/// </summary>
public sealed class OnnxEmbeddingProviderContractTests : EmbeddingProviderContractTests, IDisposable
{
    private readonly List<OnnxEmbeddingProvider> _providers = [];

    public void Dispose()
    {
        foreach (OnnxEmbeddingProvider provider in _providers)
        {
            provider.Dispose();
        }
    }

    private protected override IEmbeddingProvider CreateProvider()
    {
        OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();
        _providers.Add(provider);
        return provider;
    }
}

/// <summary>
/// Behaviour of the built-in model itself: determinism, dimension and coarse
/// semantic ordering in English and French. Skipped visibly without a model.
/// </summary>
public sealed class OnnxEmbeddingProviderModelTests
{
    private const string LightPassage =
        "Physics of everyday light: how a prism splits white light into the colours of the rainbow.";

    private const string UnrelatedPassage =
        "Quarterly tax returns for small businesses must be filed before the deadline.";

    [SkippableFact]
    public async Task Onnx_SameInputTwice_GivesIdenticalVectors()
    {
        using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();

        EmbeddingResult first = await provider.EmbedAsync(LightPassage, EmbeddingInputKind.Passage, CancellationToken.None);
        EmbeddingResult second = await provider.EmbedAsync(LightPassage, EmbeddingInputKind.Passage, CancellationToken.None);

        Assert.Equal(first.Vector, second.Vector);
    }

    [SkippableFact]
    public async Task Onnx_Dimension_EqualsManifest()
    {
        using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();
        EmbeddingModelManifest manifest = EmbeddingModelManifest.LoadDefault();

        EmbeddingResult result = await provider.EmbedAsync("light and colour", EmbeddingInputKind.Query, CancellationToken.None);

        Assert.Equal(manifest.Dimensions, provider.Dimensions);
        Assert.Equal(manifest.Dimensions, result.Vector.Length);
        Assert.Equal(manifest.ModelId, result.ModelId);
        Assert.Equal(manifest.Revision, result.ModelVersion);
    }

    [SkippableFact]
    public async Task Onnx_ConceptualQuery_PrefersRelatedPassage()
    {
        using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();

        float[] query = (await provider.EmbedAsync("light and colour", EmbeddingInputKind.Query, CancellationToken.None)).Vector;
        float[] related = (await provider.EmbedAsync(LightPassage, EmbeddingInputKind.Passage, CancellationToken.None)).Vector;
        float[] unrelated = (await provider.EmbedAsync(UnrelatedPassage, EmbeddingInputKind.Passage, CancellationToken.None)).Vector;

        Assert.True(
            Cosine(query, related) > Cosine(query, unrelated),
            $"related {Cosine(query, related):F3} should exceed unrelated {Cosine(query, unrelated):F3}");
    }

    [SkippableFact]
    public async Task Onnx_FrenchQuery_PrefersRelatedFrenchPassage()
    {
        using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();

        float[] query = (await provider.EmbedAsync("royaumes précoloniaux", EmbeddingInputKind.Query, CancellationToken.None)).Vector;
        float[] related = (await provider.EmbedAsync(
            "Les royaumes des Grands Lacs avant la colonisation : Buganda, Bunyoro et Rwanda.",
            EmbeddingInputKind.Passage,
            CancellationToken.None)).Vector;
        float[] unrelated = (await provider.EmbedAsync(
            "Recette de la tarte aux pommes avec une pâte brisée maison.",
            EmbeddingInputKind.Passage,
            CancellationToken.None)).Vector;

        Assert.True(
            Cosine(query, related) > Cosine(query, unrelated),
            $"related {Cosine(query, related):F3} should exceed unrelated {Cosine(query, unrelated):F3}");
    }

    [SkippableFact]
    public async Task Onnx_BatchCancelledBetweenInputs_StopsBeforeNextInput()
    {
        using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();
        using var cancellation = new CancellationTokenSource();
        int completed = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.EmbedBatchAsync(
            ["first input", "second input", "third input"],
            EmbeddingInputKind.Passage,
            onEmbedded: _ =>
            {
                completed++;
                cancellation.Cancel();
            },
            cancellation.Token));

        Assert.Equal(1, completed);
    }

    [SkippableFact]
    public async Task Onnx_VeryLongInput_IsTruncatedNotRejected()
    {
        using OnnxEmbeddingProvider provider = EmbeddingModelTestFolder.CreateProviderOrSkip();
        string longText = string.Join(' ', Enumerable.Repeat("prism light colour spectrum", 600));

        EmbeddingResult result = await provider.EmbedAsync(longText, EmbeddingInputKind.Passage, CancellationToken.None);

        Assert.Equal(provider.Dimensions, result.Vector.Length);
    }

    private static double Cosine(float[] left, float[] right)
    {
        double dot = 0;
        for (int i = 0; i < left.Length; i++)
        {
            dot += (double)left[i] * right[i];
        }

        return dot;
    }
}
