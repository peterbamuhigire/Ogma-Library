using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Provenance used by test fixtures that seed vectors as if a local Ollama
/// provider produced them. Production code takes provenance from the provider.
/// </summary>
internal static class TestEmbeddingIdentity
{
    public const string ProviderKey = "ollama";

    public const string ModelId = "nomic-embed-text";

    public const string ModelVersion = "nomic-embed-text:latest";
}

/// <summary>
/// Base for in-memory embedding providers. Provenance defaults to
/// <see cref="TestEmbeddingIdentity"/>; subclasses supply the vector.
/// </summary>
internal abstract class FakeEmbeddingProvider : IEmbeddingProvider
{
    public virtual string ProviderKey => TestEmbeddingIdentity.ProviderKey;

    public virtual bool IsLocalOnly => true;

    public virtual string ModelId => TestEmbeddingIdentity.ModelId;

    public virtual string ModelVersion => TestEmbeddingIdentity.ModelVersion;

    public virtual int Dimensions => 3;

    public virtual Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public virtual Task<EmbeddingResult> EmbedAsync(
        string text,
        EmbeddingInputKind kind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new EmbeddingResult(
            ProviderKey,
            ModelId,
            ModelVersion,
            CreateVector(text, kind)));
    }

    protected abstract float[] CreateVector(string text, EmbeddingInputKind kind);
}

/// <summary>
/// A deterministic, non-Ollama provider: hashes characters into
/// <see cref="Dimensions"/> buckets and returns a unit-norm vector.
/// </summary>
internal sealed class HashingEmbeddingProvider : FakeEmbeddingProvider
{
    public const string Key = "fake-hashing";

    public const string Model = "fake-hashing-e5";

    public const string Version = "fake-hashing-e5@rev-1";

    private readonly int _dimensions;

    public HashingEmbeddingProvider(int dimensions = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);
        _dimensions = dimensions;
    }

    public bool Available { get; set; } = true;

    public List<EmbeddingInputKind> Kinds { get; } = [];

    public override string ProviderKey => Key;

    public override string ModelId => Model;

    public override string ModelVersion => Version;

    public override int Dimensions => _dimensions;

    public override Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Available);

    protected override float[] CreateVector(string text, EmbeddingInputKind kind)
    {
        Kinds.Add(kind);
        var vector = new float[_dimensions];
        foreach (char c in text)
        {
            vector[c % _dimensions] += 1f;
        }

        vector[0] += 1f;
        double norm = Math.Sqrt(vector.Sum(value => (double)value * value));
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }
}
