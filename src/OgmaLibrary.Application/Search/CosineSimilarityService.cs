using System.Numerics;

namespace OgmaLibrary.Application.Search;

/// <summary>
/// Brute-force vector scoring for Phase 11 semantic search. Vectors are treated
/// as local derived index data and ranked deterministically by score then chunk.
/// </summary>
public static class CosineSimilarityService
{
    /// <summary>
    /// Computes cosine similarity in [-1, 1]. Zero vectors return 0 to avoid
    /// propagating NaN into ranking.
    /// </summary>
    public static float Score(float[] left, float[] right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension.", nameof(right));
        }

        if (left.Length == 0)
        {
            return 0f;
        }

        (float dot, float leftNormSquared, float rightNormSquared) = Accumulate(left, right);
        if (leftNormSquared <= 0f || rightNormSquared <= 0f)
        {
            return 0f;
        }

        return dot / MathF.Sqrt(leftNormSquared * rightNormSquared);
    }

    /// <summary>
    /// Returns the highest-scoring chunk vectors for a query. Ties are resolved
    /// by chunk id for reproducible ordering.
    /// </summary>
    public static IReadOnlyList<VectorSearchHit> TopK(
        float[] query,
        IEnumerable<VectorSearchCandidate> corpus,
        int k)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);

        return corpus
            .Select(candidate => new VectorSearchHit(
                candidate.ChunkId,
                Score(query, candidate.Vector)))
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.ChunkId)
            .Take(k)
            .ToList();
    }

    private static (float Dot, float LeftNormSquared, float RightNormSquared) Accumulate(
        ReadOnlySpan<float> left,
        ReadOnlySpan<float> right)
    {
        int width = Vector<float>.Count;
        int i = 0;
        Vector<float> dot = Vector<float>.Zero;
        Vector<float> leftNorm = Vector<float>.Zero;
        Vector<float> rightNorm = Vector<float>.Zero;

        for (; i <= left.Length - width; i += width)
        {
            var leftVector = new Vector<float>(left.Slice(i, width));
            var rightVector = new Vector<float>(right.Slice(i, width));
            dot += leftVector * rightVector;
            leftNorm += leftVector * leftVector;
            rightNorm += rightVector * rightVector;
        }

        float dotScalar = Vector.Sum(dot);
        float leftNormScalar = Vector.Sum(leftNorm);
        float rightNormScalar = Vector.Sum(rightNorm);

        for (; i < left.Length; i++)
        {
            dotScalar += left[i] * right[i];
            leftNormScalar += left[i] * left[i];
            rightNormScalar += right[i] * right[i];
        }

        return (dotScalar, leftNormScalar, rightNormScalar);
    }
}

/// <summary>Candidate vector loaded from the semantic index.</summary>
public sealed record VectorSearchCandidate(long ChunkId, float[] Vector);

/// <summary>Cosine-scored vector search hit.</summary>
public sealed record VectorSearchHit(long ChunkId, float Score);
