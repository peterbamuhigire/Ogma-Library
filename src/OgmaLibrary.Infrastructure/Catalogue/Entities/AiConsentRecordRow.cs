namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>EF Core row for AI consent records.</summary>
public sealed class AiConsentRecordRow
{
    /// <summary>Stable consent identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Privacy tier covered by this consent.</summary>
    public int Tier { get; set; }

    /// <summary>Provider key covered by this consent.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Consent scope.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>UTC timestamp when consent was granted.</summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>UTC timestamp when consent was revoked, if any.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
