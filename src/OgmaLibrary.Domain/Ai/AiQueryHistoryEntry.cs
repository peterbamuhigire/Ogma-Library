namespace OgmaLibrary.Domain.Ai;

/// <summary>
/// Erasable AI query history entry. Audit events remain immutable separately.
/// </summary>
public sealed record AiQueryHistoryEntry
{
    /// <summary>Creates an AI query history entry.</summary>
    public AiQueryHistoryEntry(
        string id,
        DateTimeOffset occurredAt,
        string queryType,
        string? queryText,
        string? responseSummary,
        bool deleted = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);

        Id = id;
        OccurredAt = occurredAt;
        QueryType = queryType;
        QueryText = queryText;
        ResponseSummary = responseSummary;
        Deleted = deleted;
    }

    /// <summary>Stable query-history identifier.</summary>
    public string Id { get; }

    /// <summary>UTC timestamp when the query occurred.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Query type, for example recommendation, reading-plan, or answer.</summary>
    public string QueryType { get; }

    /// <summary>User query text, if retained.</summary>
    public string? QueryText { get; }

    /// <summary>Short AI response summary, if retained.</summary>
    public string? ResponseSummary { get; }

    /// <summary>Whether this history row has been soft-deleted.</summary>
    public bool Deleted { get; }
}
