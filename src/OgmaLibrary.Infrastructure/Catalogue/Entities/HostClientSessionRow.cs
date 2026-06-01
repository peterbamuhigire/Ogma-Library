namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>EF row for LAN Host client-session metadata.</summary>
public sealed class HostClientSessionRow
{
    /// <summary>SHA-256 hash of the session token. Raw tokens are never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Client identity established during LAN enrollment.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Role assigned to the client session.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>UTC issue timestamp.</summary>
    public DateTimeOffset IssuedUtc { get; set; }

    /// <summary>UTC expiry timestamp.</summary>
    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>UTC revocation timestamp, when revoked.</summary>
    public DateTimeOffset? RevokedUtc { get; set; }
}
