using System.Numerics;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 WP3 cosine similarity and deterministic top-K tests.
/// </summary>
public sealed class CosineSimilarityServiceTests
{
    [Fact]
    public void Cosine_IdenticalVectors_ReturnsOne()
    {
        float score = CosineSimilarityService.Score([1f, 0f, 0f], [1f, 0f, 0f]);

        Assert.Equal(1f, score, precision: 6);
    }

    [Fact]
    public void Cosine_OppositeVectors_ReturnsMinusOne()
    {
        float score = CosineSimilarityService.Score([1f, 0f, 0f], [-1f, 0f, 0f]);

        Assert.Equal(-1f, score, precision: 6);
    }

    [Fact]
    public void Cosine_OrthogonalVectors_ReturnsZero()
    {
        float score = CosineSimilarityService.Score([1f, 0f, 0f], [0f, 1f, 0f]);

        Assert.Equal(0f, score, precision: 6);
    }

    [Fact]
    public void Cosine_ZeroVector_ReturnsZero()
    {
        float score = CosineSimilarityService.Score([0f, 0f, 0f], [1f, 2f, 3f]);

        Assert.Equal(0f, score, precision: 6);
    }

    [Fact]
    public void TopK_ReturnsHighestScoresInDeterministicOrder()
    {
        IReadOnlyList<VectorSearchHit> hits = CosineSimilarityService.TopK(
            [1f, 0f],
            [
                new VectorSearchCandidate(30, [0f, 1f]),
                new VectorSearchCandidate(20, [1f, 0f]),
                new VectorSearchCandidate(10, [1f, 0f]),
                new VectorSearchCandidate(40, [-1f, 0f]),
            ],
            3);

        Assert.Equal([10, 20, 30], hits.Select(hit => hit.ChunkId).ToArray());
        Assert.Equal(1f, hits[0].Score, precision: 6);
        Assert.Equal(0f, hits[2].Score, precision: 6);
    }

    [Fact]
    public void TopK_SimdWidthVectors_MatchExpectedOrdering()
    {
        int dimension = Math.Max(16, Vector<float>.Count * 2 + 3);
        float[] query = Enumerable.Repeat(0f, dimension).ToArray();
        query[0] = 1f;
        float[] close = Enumerable.Repeat(0.01f, dimension).ToArray();
        close[0] = 1f;
        float[] far = Enumerable.Repeat(1f, dimension).ToArray();

        IReadOnlyList<VectorSearchHit> hits = CosineSimilarityService.TopK(
            query,
            [
                new VectorSearchCandidate(2, far),
                new VectorSearchCandidate(1, close),
            ],
            2);

        Assert.Equal(1, hits[0].ChunkId);
        Assert.True(hits[0].Score > hits[1].Score);
    }
}
