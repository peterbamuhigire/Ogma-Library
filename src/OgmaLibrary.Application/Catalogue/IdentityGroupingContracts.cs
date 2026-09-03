namespace OgmaLibrary.Application.Catalogue;

/// <summary>The identity level at which occurrences are grouped.</summary>
public enum IdentityGroupKind
{
    /// <summary>Different editions that belong to one intellectual work.</summary>
    Work = 0,

    /// <summary>Byte or bibliographically equivalent occurrences of one edition.</summary>
    Edition = 1,
}

/// <summary>Durable description of a reviewed identity group.</summary>
public sealed record IdentityGroupDescriptor(
    string GroupId,
    IdentityGroupKind Kind,
    IReadOnlyList<string> ActiveOccurrenceIds,
    int Version,
    DateTimeOffset UpdatedUtc);

/// <summary>Legacy-book projection of a reviewed group membership.</summary>
public sealed record IdentityGroupBookMembership(
    string BookId,
    string GroupId,
    IdentityGroupKind Kind);

/// <summary>Audited, reversible identity grouping operations.</summary>
public interface IIdentityGroupingService
{
    /// <summary>Creates an initial group from reviewed occurrence identities.</summary>
    Task<IdentityGroupDescriptor> CreateAsync(
        IdentityGroupKind kind,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        CancellationToken cancellationToken = default);

    /// <summary>Adds reviewed occurrences to a group and records the before-image.</summary>
    Task<IdentityGroupDescriptor> MergeAsync(
        string groupId,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        CancellationToken cancellationToken = default);

    /// <summary>Removes reviewed occurrences while retaining a reversible before-image.</summary>
    Task<IdentityGroupDescriptor> SplitAsync(
        string groupId,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        CancellationToken cancellationToken = default);

    /// <summary>Restores the latest merge or split operation exactly once.</summary>
    Task<IdentityGroupDescriptor> UndoLastAsync(
        string groupId,
        string actor,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a deterministic group projection for an occurrence.</summary>
    Task<IdentityGroupDescriptor?> FindByOccurrenceAsync(
        string occurrenceId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves reviewed groups for legacy book IDs through canonical aliases.</summary>
    Task<IReadOnlyList<IdentityGroupBookMembership>> FindBookMembershipsAsync(
        IReadOnlyList<string> bookIds,
        bool includeWorkGroups,
        CancellationToken cancellationToken = default);
}
