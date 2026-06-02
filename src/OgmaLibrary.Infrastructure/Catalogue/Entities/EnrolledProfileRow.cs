namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>School-managed classroom profile enrollment metadata.</summary>
public sealed class EnrolledProfileRow
{
    /// <summary>Stable classroom profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Display name shown to administrators and teachers.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Assigned classroom role: student, teacher, or admin.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Optional birth year. Null means treat as minor for DPIA screening.</summary>
    public int? BirthYear { get; set; }

    /// <summary>One-time enrollment token hash or opaque token value until token hashing lands.</summary>
    public string? EnrollmentToken { get; set; }

    /// <summary>UTC enrollment token expiry timestamp.</summary>
    public DateTimeOffset? EnrollmentTokenExpiresUtc { get; set; }

    /// <summary>UTC creation/enrollment timestamp.</summary>
    public DateTimeOffset EnrolledUtc { get; set; }

    /// <summary>UTC revocation timestamp, when revoked.</summary>
    public DateTimeOffset? RevokedUtc { get; set; }
}
