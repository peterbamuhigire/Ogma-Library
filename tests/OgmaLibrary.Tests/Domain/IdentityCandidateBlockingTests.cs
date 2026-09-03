using OgmaLibrary.Domain;

namespace OgmaLibrary.Tests.Domain;

/// <summary>Correctness and scale tests for Phase 9 candidate blocking.</summary>
public sealed class IdentityCandidateBlockingTests
{
    [Fact]
    public void Build_UsesScopedIdentifiersAndNormalizedBibliographicKeys()
    {
        IdentityEvidenceProfile first = Profile(1, "The Art of Reading", "Nora Reader", 2024);
        IdentityEvidenceProfile second = Profile(2, " the   art of reading ", "nora reader", 2024);
        IdentityEvidenceProfile unrelated = Profile(3, "A Different Book", "Other Author", 2024);

        IReadOnlyList<IdentityCandidatePair> pairs = IdentityCandidateBlocking.Build(
            [first, second, unrelated]);

        IdentityCandidatePair pair = Assert.Single(pairs);
        Assert.Equal(first.OccurrenceId, pair.Subject.OccurrenceId);
        Assert.Equal(second.OccurrenceId, pair.Candidate.OccurrenceId);
        Assert.StartsWith("bibliographic|", pair.BlockKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DeduplicatesPairsAndBoundsPathologicalBuckets()
    {
        IdentityEvidenceProfile[] profiles = Enumerable.Range(1, 10_000)
            .Select(index => Profile(index, "Common Title", "Common Author", 2024))
            .ToArray();

        IReadOnlyList<IdentityCandidatePair> pairs = IdentityCandidateBlocking.Build(
            profiles,
            maximumBucketSize: 64);

        Assert.Equal(64 * 63 / 2, pairs.Count);
        Assert.Equal(
            pairs.Count,
            pairs.Select(pair => (pair.Subject.OccurrenceId, pair.Candidate.OccurrenceId)).Distinct().Count());
        Assert.Equal(
            pairs.OrderBy(pair => pair.Subject.OccurrenceId.Value, StringComparer.Ordinal)
                .ThenBy(pair => pair.Candidate.OccurrenceId.Value, StringComparer.Ordinal)
                .Select(pair => pair.Subject.OccurrenceId.Value),
            pairs.Select(pair => pair.Subject.OccurrenceId.Value));
    }

    private static IdentityEvidenceProfile Profile(
        int index,
        string title,
        string author,
        int year) => new(
        new FileOccurrenceId($"01P09{index:D21}"),
        contentHash: null,
        normalizedTitle: title,
        normalizedAuthors: [author],
        publicationYear: year);
}
