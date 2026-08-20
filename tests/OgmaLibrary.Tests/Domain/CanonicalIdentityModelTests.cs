using OgmaLibrary.Domain;

namespace OgmaLibrary.Tests.Domain;

/// <summary>Executable invariants for the Phase 3 canonical identity freeze candidate.</summary>
public sealed class CanonicalIdentityModelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("contains space")]
    public void StableIdentity_RejectsMissingOrWhitespaceValues(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new WorkId(value!));
    }

    [Fact]
    public void FileOccurrence_CanRepresentUnknownContentWithoutInventingAnAsset()
    {
        var occurrence = new FileOccurrence(
            new FileOccurrenceId("occurrence-1"),
            new LibraryRootId("root-1"),
            contentAssetId: null,
            AvailabilityStatus.Unavailable);

        Assert.Null(occurrence.ContentAssetId);
        Assert.Equal(AvailabilityStatus.Unavailable, occurrence.Availability);
    }

    [Fact]
    public void CanonicalEntities_RejectDefaultStrongIdentitiesAndHashes()
    {
        Assert.Throws<ArgumentException>(() => new Work(default, BibliographicResolutionState.Unknown));
        Assert.Throws<ArgumentException>(() => new Edition(default, new WorkId("work-1"), BibliographicResolutionState.Unknown));
        Assert.Throws<ArgumentException>(() => new FileOccurrence(default, new LibraryRootId("root-1"), null, AvailabilityStatus.Available));
        Assert.ThrowsAny<ArgumentException>(() => new ContentAsset(new ContentAssetId("asset-1"), default, 1, 10));
        Assert.Throws<ArgumentException>(() => new CataloguePresentationIdentity(default, new WorkId("work-1"), new EditionId("edition-1"), null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Work(new WorkId("work-1"), (BibliographicResolutionState)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileOccurrence(
            new FileOccurrenceId("occurrence-1"),
            new LibraryRootId("root-1"),
            null,
            (AvailabilityStatus)99));
    }

    [Fact]
    public void Work_GroupsOnlyEditionsThatBelongToIt()
    {
        var work = new Work(new WorkId("work-1"), BibliographicResolutionState.Identified);
        var ownEdition = new Edition(new EditionId("edition-1"), work.Id, BibliographicResolutionState.Identified);
        var otherEdition = new Edition(new EditionId("edition-2"), new WorkId("work-2"), BibliographicResolutionState.Identified);

        Assert.True(work.AddEdition(ownEdition));
        Assert.False(work.AddEdition(ownEdition));
        Assert.Throws<ArgumentException>(() => work.AddEdition(otherEdition));
    }

    [Fact]
    public void BibliographicIdentifiers_EnforceWorkAndEditionScopes()
    {
        Assert.Throws<ArgumentException>(() => new BibliographicIdentifier(
            "isbn",
            BibliographicIdentifierKind.Isbn13,
            BibliographicIdentityScope.Work,
            "9780000000002"));
        Assert.Throws<ArgumentException>(() => new BibliographicIdentifier(
            "provider",
            BibliographicIdentifierKind.ProviderWorkId,
            BibliographicIdentityScope.Edition,
            "work-42"));
        Assert.Throws<ArgumentException>(() => new IdentityEvidenceProfile(
            new FileOccurrenceId("occurrence-1"),
            null,
            [default(BibliographicIdentifier)]));
    }

    [Fact]
    public void ExactHash_IsTheOnlyAutomaticRelationship()
    {
        for (int index = 0; index < 64; index++)
        {
            ContentHash hash = ContentHash.Compute(BitConverter.GetBytes(index));
            IdentityDecision decision = Evaluate(hash, hash);

            Assert.Equal(IdentityRelationship.ExactContentCopy, decision.Relationship);
            Assert.Equal(IdentityDecisionDisposition.Automatic, decision.Disposition);
            Assert.Equal(IdentityDecisionTier.ContentHash, decision.Tier);
            Assert.Equal(1, decision.Confidence.Value);
        }
    }

    [Fact]
    public void SharedEditionIdentifier_IsReviewableSameEditionDifferentAsset()
    {
        BibliographicIdentifier isbn = EditionIdentifier("9780000000002");
        IdentityDecision decision = Evaluate(
            ContentHash.Compute([1]),
            ContentHash.Compute([2]),
            [isbn],
            [isbn]);

        Assert.Equal(IdentityRelationship.SameEditionDifferentAsset, decision.Relationship);
        Assert.Equal(IdentityDecisionDisposition.ReviewRequired, decision.Disposition);
        Assert.Equal(IdentityDecisionTier.EditionIdentifier, decision.Tier);
    }

    [Fact]
    public void SharedWorkAndConflictingEditionIdentifiers_IsSameWorkDifferentEdition()
    {
        BibliographicIdentifier work = WorkIdentifier("provider-work-1");
        IdentityDecision decision = Evaluate(
            ContentHash.Compute([1]),
            ContentHash.Compute([2]),
            [work, EditionIdentifier("9780000000002")],
            [work, EditionIdentifier("9780000000019")]);

        Assert.Equal(IdentityRelationship.SameWorkDifferentEdition, decision.Relationship);
        Assert.Equal(IdentityDecisionDisposition.ReviewRequired, decision.Disposition);
    }

    [Fact]
    public void ContradictoryEditionIdentifiers_RemainAmbiguousAndReviewable()
    {
        BibliographicIdentifier shared = EditionIdentifier("9780000000002");
        IdentityDecision decision = Evaluate(
            ContentHash.Compute([1]),
            ContentHash.Compute([2]),
            [shared, EditionIdentifier("9780000000019")],
            [shared, EditionIdentifier("9780000000026")]);

        Assert.Equal(IdentityRelationship.PossibleMatch, decision.Relationship);
        Assert.Equal(IdentityDecisionDisposition.ReviewRequired, decision.Disposition);
        Assert.Equal(IdentityDecisionTier.EditionIdentifier, decision.Tier);
    }

    [Fact]
    public void SimilarTitleOnly_NeverSilentlyMerges()
    {
        var subject = new IdentityEvidenceProfile(
            new FileOccurrenceId("occurrence-1"),
            null,
            titleAuthorSimilarity: new ConfidenceScore(0.99));
        var candidate = new IdentityEvidenceProfile(
            new FileOccurrenceId("occurrence-2"),
            null,
            titleAuthorSimilarity: new ConfidenceScore(0.99));

        IdentityDecision decision = IdentityDecisionPolicy.Evaluate(
            new IdentityDecisionId("decision-1"),
            subject,
            candidate);

        Assert.Equal(IdentityRelationship.PossibleMatch, decision.Relationship);
        Assert.Equal(IdentityDecisionDisposition.ReviewRequired, decision.Disposition);
        Assert.NotEqual(IdentityRelationship.SameEditionDifferentAsset, decision.Relationship);
    }

    [Fact]
    public void ProviderIdentifiers_FromDifferentNamespaces_DoNotMatch()
    {
        var left = new BibliographicIdentifier(
            "provider-a",
            BibliographicIdentifierKind.ProviderEditionId,
            BibliographicIdentityScope.Edition,
            "42");
        var right = new BibliographicIdentifier(
            "provider-b",
            BibliographicIdentifierKind.ProviderEditionId,
            BibliographicIdentityScope.Edition,
            "42");

        IdentityDecision decision = Evaluate(null, null, [left], [right]);

        Assert.Equal(IdentityRelationship.Unknown, decision.Relationship);
        Assert.Equal(IdentityDecisionDisposition.ReviewRequired, decision.Disposition);
    }

    [Fact]
    public void IdentityPolicy_RejectsSelfComparisonAndDefaultDecisionId()
    {
        var profile = new IdentityEvidenceProfile(new FileOccurrenceId("occurrence-1"), null);
        var other = new IdentityEvidenceProfile(new FileOccurrenceId("occurrence-2"), null);

        Assert.Throws<ArgumentException>(() => IdentityDecisionPolicy.Evaluate(
            new IdentityDecisionId("decision-1"), profile, profile));
        Assert.Throws<ArgumentException>(() => IdentityDecisionPolicy.Evaluate(default, profile, other));
    }

    private static IdentityDecision Evaluate(
        ContentHash? subjectHash,
        ContentHash? candidateHash,
        IEnumerable<BibliographicIdentifier>? subjectIdentifiers = null,
        IEnumerable<BibliographicIdentifier>? candidateIdentifiers = null) =>
        IdentityDecisionPolicy.Evaluate(
            new IdentityDecisionId("decision-1"),
            new IdentityEvidenceProfile(new FileOccurrenceId("occurrence-1"), subjectHash, subjectIdentifiers),
            new IdentityEvidenceProfile(new FileOccurrenceId("occurrence-2"), candidateHash, candidateIdentifiers));

    private static BibliographicIdentifier EditionIdentifier(string value) => new(
        "isbn",
        BibliographicIdentifierKind.Isbn13,
        BibliographicIdentityScope.Edition,
        value);

    private static BibliographicIdentifier WorkIdentifier(string value) => new(
        "provider",
        BibliographicIdentifierKind.ProviderWorkId,
        BibliographicIdentityScope.Work,
        value);
}
