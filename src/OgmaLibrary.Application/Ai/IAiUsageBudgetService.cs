namespace OgmaLibrary.Application.Ai;

/// <summary>Daily token and cost limits enforced by the AI gateway.</summary>
public sealed record AiUsageBudgetLimits(
    int DailyTokenLimit = 100_000,
    decimal DailyCostUsdLimit = 10m)
{
    /// <summary>Validates the local budget policy.</summary>
    public void Validate()
    {
        if (DailyTokenLimit is < 1 or > 10_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(DailyTokenLimit));
        }

        if (DailyCostUsdLimit < 0m || DailyCostUsdLimit > 1_000_000m)
        {
            throw new ArgumentOutOfRangeException(nameof(DailyCostUsdLimit));
        }
    }
}

/// <summary>Durable daily usage counters exposed for UI and diagnostics.</summary>
public sealed record AiUsageBudgetSnapshot(
    DateOnly UtcDate,
    int ReservedTokens,
    int UsedTokens,
    decimal UsedCostUsd,
    AiUsageBudgetLimits Limits)
{
    /// <summary>Tokens still available after reservations and usage.</summary>
    public int RemainingTokens => Math.Max(0, Limits.DailyTokenLimit - ReservedTokens - UsedTokens);

    /// <summary>Cost still available under the daily limit.</summary>
    public decimal RemainingCostUsd => Math.Max(0m, Limits.DailyCostUsdLimit - UsedCostUsd);
}

/// <summary>One pre-call token reservation held until a completion is reconciled.</summary>
public sealed record AiUsageBudgetReservation(string Id, int EstimatedTokens);

/// <summary>Durable usage budget enforced at the AI gateway boundary.</summary>
public interface IAiUsageBudgetService
{
    /// <summary>Returns the current UTC-day budget snapshot.</summary>
    Task<AiUsageBudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Reserves bounded tokens before a provider request is sent.</summary>
    Task<AiUsageBudgetReservation> ReserveAsync(
        AiRequest request,
        CancellationToken cancellationToken);

    /// <summary>Reconciles a reservation with provider-reported usage and cost.</summary>
    Task FinalizeAsync(
        AiUsageBudgetReservation reservation,
        AiCompletion completion,
        decimal? costUsd,
        CancellationToken cancellationToken);

    /// <summary>Releases a reservation when no provider completion was produced.</summary>
    Task ReleaseAsync(
        AiUsageBudgetReservation reservation,
        CancellationToken cancellationToken);
}

/// <summary>Raised before provider execution when the daily budget is exhausted.</summary>
public sealed class AiUsageBudgetExceededException : InvalidOperationException
{
    /// <summary>Initializes a budget-exceeded failure.</summary>
    public AiUsageBudgetExceededException(string message)
        : base(message)
    {
    }
}
