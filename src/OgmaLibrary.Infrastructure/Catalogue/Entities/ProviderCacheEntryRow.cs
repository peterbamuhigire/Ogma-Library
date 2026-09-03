namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Bounded durable cache entry for one normalized provider query.</summary>
public sealed class ProviderCacheEntryRow
{
    /// <summary>Database identifier.</summary>
    public long ProviderCacheEntryId { get; set; }

    /// <summary>Provider stable name.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Normalized query key without user secrets.</summary>
    public string QueryKey { get; set; } = string.Empty;

    /// <summary>Serialized bounded provider result list.</summary>
    public string ResponseJson { get; set; } = string.Empty;

    /// <summary>Whether the provider returned no result.</summary>
    public bool IsNegative { get; set; }

    /// <summary>UTC retrieval timestamp.</summary>
    public DateTimeOffset RetrievedUtc { get; set; }

    /// <summary>UTC expiry timestamp.</summary>
    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>Provider response contract version.</summary>
    public int ContractVersion { get; set; }

    /// <summary>Provider validator used for conditional revalidation.</summary>
    public string? ETag { get; set; }
}
