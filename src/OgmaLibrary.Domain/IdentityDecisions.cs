namespace OgmaLibrary.Domain;

/// <summary>The relationship supported by identity evidence.</summary>
public enum IdentityRelationship
{
    /// <summary>Both occurrences contain exactly the same bytes.</summary>
    ExactContentCopy = 0,

    /// <summary>Different byte assets represent the same publication.</summary>
    SameEditionDifferentAsset = 1,

    /// <summary>Different publications belong to the same intellectual work.</summary>
    SameWorkDifferentEdition = 2,

    /// <summary>Evidence suggests a relationship but cannot establish its level.</summary>
    PossibleMatch = 3,

    /// <summary>Available evidence does not establish a relationship.</summary>
    Unknown = 4,
}

/// <summary>Whether a decision may apply automatically or requires review.</summary>
public enum IdentityDecisionDisposition
{
    /// <summary>Deterministic evidence permits an automatic exact-copy link.</summary>
    Automatic = 0,

    /// <summary>A user must review the proposed bibliographic relationship.</summary>
    ReviewRequired = 1,
}

/// <summary>The highest evidence tier used by an identity decision.</summary>
public enum IdentityDecisionTier
{
    /// <summary>Verified complete-file content hashes.</summary>
    ContentHash = 0,

    /// <summary>Edition-scoped ISBN, DOI or provider identifiers.</summary>
    EditionIdentifier = 1,

    /// <summary>Work-scoped provider identifiers.</summary>
    WorkIdentifier = 2,

    /// <summary>Normalized title/author similarity only.</summary>
    BibliographicSimilarity = 3,

    /// <summary>No evidence tier established identity.</summary>
    InsufficientEvidence = 4,
}

/// <summary>Path-free evidence used to compare two file occurrences.</summary>
public sealed class IdentityEvidenceProfile
{
    /// <summary>Initializes an immutable evidence profile.</summary>
    public IdentityEvidenceProfile(
        FileOccurrenceId occurrenceId,
        ContentHash? contentHash,
        IEnumerable<BibliographicIdentifier>? identifiers = null,
        ConfidenceScore? titleAuthorSimilarity = null)
    {
        StableIdentity.EnsureDefined(occurrenceId.Value, nameof(occurrenceId));
        if (contentHash is ContentHash hash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash.Hex, nameof(contentHash));
        }

        BibliographicIdentifier[] materializedIdentifiers = identifiers?.Distinct().ToArray() ?? [];
        if (materializedIdentifiers.Any(identifier =>
                string.IsNullOrWhiteSpace(identifier.Source) ||
                string.IsNullOrWhiteSpace(identifier.Value)))
        {
            throw new ArgumentException(
                "Default or incomplete bibliographic identifiers are not evidence.",
                nameof(identifiers));
        }

        OccurrenceId = occurrenceId;
        ContentHash = contentHash;
        Identifiers = materializedIdentifiers;
        TitleAuthorSimilarity = titleAuthorSimilarity;
    }

    /// <summary>The file occurrence being evaluated.</summary>
    public FileOccurrenceId OccurrenceId { get; }

    /// <summary>The complete-file hash, or null while exact content is unknown.</summary>
    public ContentHash? ContentHash { get; }

    /// <summary>Scoped bibliographic evidence.</summary>
    public IReadOnlyList<BibliographicIdentifier> Identifiers { get; }

    /// <summary>Optional normalized title-and-author similarity.</summary>
    public ConfidenceScore? TitleAuthorSimilarity { get; }
}

/// <summary>A versioned, path-free identity decision suitable for persistence and audit.</summary>
public sealed record IdentityDecision(
    IdentityDecisionId Id,
    FileOccurrenceId SubjectOccurrenceId,
    FileOccurrenceId CandidateOccurrenceId,
    IdentityRelationship Relationship,
    IdentityDecisionDisposition Disposition,
    IdentityDecisionTier Tier,
    ConfidenceScore Confidence,
    int PolicyVersion);

/// <summary>
/// Conservative identity policy: only equal complete-file hashes apply
/// automatically. Bibliographic and similarity evidence always produces a
/// reviewable proposal.
/// </summary>
public static class IdentityDecisionPolicy
{
    /// <summary>The persisted compatibility version of this policy.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Evaluates path-free evidence without silently merging ambiguous records.</summary>
    public static IdentityDecision Evaluate(
        IdentityDecisionId decisionId,
        IdentityEvidenceProfile subject,
        IdentityEvidenceProfile candidate)
    {
        StableIdentity.EnsureDefined(decisionId.Value, nameof(decisionId));
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(candidate);
        if (subject.OccurrenceId == candidate.OccurrenceId)
        {
            throw new ArgumentException("An occurrence cannot be compared with itself.", nameof(candidate));
        }

        if (subject.ContentHash is ContentHash subjectHash &&
            candidate.ContentHash is ContentHash candidateHash &&
            subjectHash == candidateHash)
        {
            return Create(
                decisionId,
                subject,
                candidate,
                IdentityRelationship.ExactContentCopy,
                IdentityDecisionDisposition.Automatic,
                IdentityDecisionTier.ContentHash,
                1.0);
        }

        bool sameEdition = ShareIdentifier(
            subject,
            candidate,
            BibliographicIdentityScope.Edition);
        bool conflictingEdition = HaveConflictingCanonicalEditionIdentifiers(subject, candidate);
        if (sameEdition && conflictingEdition)
        {
            return Create(
                decisionId,
                subject,
                candidate,
                IdentityRelationship.PossibleMatch,
                IdentityDecisionDisposition.ReviewRequired,
                IdentityDecisionTier.EditionIdentifier,
                0.65);
        }

        if (sameEdition)
        {
            return Create(
                decisionId,
                subject,
                candidate,
                IdentityRelationship.SameEditionDifferentAsset,
                IdentityDecisionDisposition.ReviewRequired,
                IdentityDecisionTier.EditionIdentifier,
                0.95);
        }

        bool sameWork = ShareIdentifier(
            subject,
            candidate,
            BibliographicIdentityScope.Work);
        if (sameWork && conflictingEdition)
        {
            return Create(
                decisionId,
                subject,
                candidate,
                IdentityRelationship.SameWorkDifferentEdition,
                IdentityDecisionDisposition.ReviewRequired,
                IdentityDecisionTier.WorkIdentifier,
                0.90);
        }

        if (sameWork)
        {
            return Create(
                decisionId,
                subject,
                candidate,
                IdentityRelationship.PossibleMatch,
                IdentityDecisionDisposition.ReviewRequired,
                IdentityDecisionTier.WorkIdentifier,
                0.80);
        }

        double similarity = Math.Min(
            subject.TitleAuthorSimilarity?.Value ?? 0,
            candidate.TitleAuthorSimilarity?.Value ?? 0);
        if (similarity >= 0.75)
        {
            return Create(
                decisionId,
                subject,
                candidate,
                IdentityRelationship.PossibleMatch,
                IdentityDecisionDisposition.ReviewRequired,
                IdentityDecisionTier.BibliographicSimilarity,
                Math.Min(similarity, 0.85));
        }

        return Create(
            decisionId,
            subject,
            candidate,
            IdentityRelationship.Unknown,
            IdentityDecisionDisposition.ReviewRequired,
            IdentityDecisionTier.InsufficientEvidence,
            0);
    }

    private static bool ShareIdentifier(
        IdentityEvidenceProfile subject,
        IdentityEvidenceProfile candidate,
        BibliographicIdentityScope scope) =>
        subject.Identifiers
            .Where(identifier => identifier.Scope == scope)
            .Any(left => candidate.Identifiers.Any(left.RefersToSameIdentityAs));

    private static bool HaveConflictingCanonicalEditionIdentifiers(
        IdentityEvidenceProfile subject,
        IdentityEvidenceProfile candidate)
    {
        IEnumerable<BibliographicIdentifier> leftIdentifiers = subject.Identifiers
            .Where(IsCanonicalEditionIdentifier);
        IEnumerable<BibliographicIdentifier> rightIdentifiers = candidate.Identifiers
            .Where(IsCanonicalEditionIdentifier);

        return leftIdentifiers.Any(left => rightIdentifiers.Any(right =>
            left.Kind == right.Kind &&
            ProviderNamespaceMatchesWhenRequired(left, right) &&
            !string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsCanonicalEditionIdentifier(BibliographicIdentifier identifier) =>
        identifier.Scope == BibliographicIdentityScope.Edition &&
        identifier.Kind is BibliographicIdentifierKind.Isbn10 or
                           BibliographicIdentifierKind.Isbn13 or
                           BibliographicIdentifierKind.Doi or
                           BibliographicIdentifierKind.ProviderEditionId;

    private static bool ProviderNamespaceMatchesWhenRequired(
        BibliographicIdentifier left,
        BibliographicIdentifier right) =>
        left.Kind != BibliographicIdentifierKind.ProviderEditionId ||
        string.Equals(left.Source, right.Source, StringComparison.OrdinalIgnoreCase);

    private static IdentityDecision Create(
        IdentityDecisionId decisionId,
        IdentityEvidenceProfile subject,
        IdentityEvidenceProfile candidate,
        IdentityRelationship relationship,
        IdentityDecisionDisposition disposition,
        IdentityDecisionTier tier,
        double confidence) =>
        new(
            decisionId,
            subject.OccurrenceId,
            candidate.OccurrenceId,
            relationship,
            disposition,
            tier,
            new ConfidenceScore(confidence),
            CurrentVersion);
}
