namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Per-profile classroom AI quota and rate-limit policy.</summary>
public sealed class SchoolAiEntitlementRow
{
    /// <summary>Classroom profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Daily per-profile token budget.</summary>
    public int DailyTokenBudget { get; set; }

    /// <summary>Daily class-wide token budget snapshot used by the quota service.</summary>
    public int ClassDailyTokenBudget { get; set; }

    /// <summary>Per-profile query rate limit per minute.</summary>
    public int RateLimitQueriesPerMin { get; set; }

    /// <summary>UTC update timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
