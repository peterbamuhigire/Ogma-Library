namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Daily classroom AI usage ledger used for quota enforcement and dashboarding.</summary>
public sealed class AiUsageLedgerRow
{
    /// <summary>Stable ledger row identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Classroom profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>UTC date bucket in yyyy-MM-dd form.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Total tokens used in this date bucket.</summary>
    public int TokensUsed { get; set; }

    /// <summary>Total AI queries in this date bucket.</summary>
    public int QueryCount { get; set; }

    /// <summary>Estimated total cost in USD for this date bucket.</summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>UTC update timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
