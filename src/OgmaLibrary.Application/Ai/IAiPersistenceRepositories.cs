using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>Persistence contract for AI consent records.</summary>
public interface IAiConsentRepository
{
    /// <summary>Creates or updates a consent record.</summary>
    Task UpsertAsync(AiConsentRecord consent, CancellationToken cancellationToken);

    /// <summary>Gets the active consent for tier/provider/scope, if present.</summary>
    Task<AiConsentRecord?> GetActiveConsentAsync(
        AiPrivacyTier tier,
        string provider,
        string scope,
        CancellationToken cancellationToken);

    /// <summary>Revokes all active consent records for the specified tier.</summary>
    Task<int> RevokeAllAsync(AiPrivacyTier tier, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}

/// <summary>Append-only persistence contract for AI audit events.</summary>
public interface IAiAuditRepository
{
    /// <summary>Appends an immutable audit event.</summary>
    Task AppendAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken);

    /// <summary>Reads recent audit events, newest first.</summary>
    Task<IReadOnlyList<AiAuditEvent>> GetRecentAsync(int count, CancellationToken cancellationToken);

    /// <summary>Exports audit events as JSON to a caller-owned stream.</summary>
    Task ExportToJsonAsync(Stream output, CancellationToken cancellationToken);
}

/// <summary>Persistence contract for erasable AI query history.</summary>
public interface IAiQueryHistoryRepository
{
    /// <summary>Adds a query-history entry.</summary>
    Task AddAsync(AiQueryHistoryEntry entry, CancellationToken cancellationToken);

    /// <summary>Lists non-deleted query-history entries by page.</summary>
    Task<IReadOnlyList<AiQueryHistoryEntry>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Soft-deletes a history entry.</summary>
    Task<bool> SoftDeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Hard-deletes all query-history rows without deleting audit rows.</summary>
    Task<int> HardDeleteAllAsync(CancellationToken cancellationToken);
}
