namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Durable reviewed identity group.</summary>
public sealed class IdentityGroupRow
{
    /// <summary>Canonical group identifier.</summary>
    public string IdentityGroupId { get; set; } = string.Empty;

    /// <summary>Work or edition grouping.</summary>
    public int Kind { get; set; }

    /// <summary>Optimistic version of the group.</summary>
    public int Version { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC last mutation time.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>Membership row for one occurrence in an identity group.</summary>
public sealed class IdentityGroupMemberRow
{
    /// <summary>Owning group.</summary>
    public string IdentityGroupId { get; set; } = string.Empty;

    /// <summary>Occurrence identity.</summary>
    public string FileOccurrenceId { get; set; } = string.Empty;

    /// <summary>Whether the member is currently included.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC membership update time.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>Before/after history for a reversible identity-group mutation.</summary>
public sealed class IdentityGroupChangeRow
{
    /// <summary>Database identifier.</summary>
    public long IdentityGroupChangeId { get; set; }

    /// <summary>Owning group.</summary>
    public string IdentityGroupId { get; set; } = string.Empty;

    /// <summary>Merge, split or undo.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Active members before the mutation.</summary>
    public string BeforeMembersJson { get; set; } = "[]";

    /// <summary>Active members after the mutation.</summary>
    public string AfterMembersJson { get; set; } = "[]";

    /// <summary>Non-secret actor label.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>UTC mutation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
