namespace OgmaLibrary.Application.Metadata;

/// <summary>Review lifecycle for one persisted metadata proposal.</summary>
public enum MetadataProposalStatus
{
    /// <summary>Proposal is waiting for a user decision.</summary>
    Pending = 0,

    /// <summary>Proposal was explicitly accepted and applied.</summary>
    Accepted = 1,

    /// <summary>Proposal was explicitly rejected.</summary>
    Rejected = 2,
}

/// <summary>Reviewable metadata proposal with provenance and alternatives.</summary>
public sealed record MetadataProposalDescriptor(
    long Id,
    string BookId,
    string FieldName,
    string? ProposedValue,
    string? CurrentValue,
    double Confidence,
    string Source,
    IReadOnlyList<AlternativeFieldValue> Alternatives,
    MetadataProposalStatus Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? DecidedUtc);

/// <summary>Command boundary for reviewable metadata curation.</summary>
public interface IMetadataReviewService
{
    /// <summary>Persists a batch of merged proposals as pending review items.</summary>
    Task<IReadOnlyList<MetadataProposalDescriptor>> CreateAsync(
        string bookId,
        IReadOnlyList<MergedMetadataProposal> proposals,
        CancellationToken cancellationToken = default);

    /// <summary>Lists pending proposals without loading PDF text or raw responses.</summary>
    Task<IReadOnlyList<MetadataProposalDescriptor>> ListPendingAsync(
        string bookId,
        CancellationToken cancellationToken = default);

    /// <summary>Accepts or rejects one proposal, optionally as an explicit user override.</summary>
    Task<MetadataProposalDescriptor> DecideAsync(
        long proposalId,
        bool accept,
        string? editedValue = null,
        bool userOverride = false,
        CancellationToken cancellationToken = default);
}
