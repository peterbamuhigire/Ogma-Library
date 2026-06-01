namespace OgmaLibrary.Domain.Ai;

/// <summary>
/// Consent grant for one AI privacy tier, provider, and scope.
/// </summary>
public sealed record AiConsentRecord
{
    /// <summary>Creates a consent record.</summary>
    public AiConsentRecord(
        string id,
        AiPrivacyTier tier,
        string provider,
        string scope,
        DateTimeOffset grantedAt,
        DateTimeOffset? revokedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        Id = id;
        Tier = tier;
        Provider = provider;
        Scope = scope;
        GrantedAt = grantedAt;
        RevokedAt = revokedAt;
    }

    /// <summary>Stable consent identifier.</summary>
    public string Id { get; }

    /// <summary>Privacy tier covered by this consent.</summary>
    public AiPrivacyTier Tier { get; }

    /// <summary>Provider key covered by this consent.</summary>
    public string Provider { get; }

    /// <summary>Consent scope, for example <c>library:default</c>, <c>session</c>, or <c>query</c>.</summary>
    public string Scope { get; }

    /// <summary>UTC timestamp when consent was granted.</summary>
    public DateTimeOffset GrantedAt { get; }

    /// <summary>UTC timestamp when consent was revoked, if any.</summary>
    public DateTimeOffset? RevokedAt { get; }

    /// <summary>Whether this consent is still active.</summary>
    public bool IsActive => RevokedAt is null;
}
