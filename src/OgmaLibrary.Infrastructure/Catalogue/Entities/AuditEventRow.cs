namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>
/// The EF Core persistence row for the append-only local audit trail
/// (HLD §3 — AuditEvents table, NFR-PROD-013, CTRL-OGMA-018).
/// Rows are never updated or deleted — the repository contract enforces this.
/// </summary>
public sealed class AuditEventRow
{
    /// <summary>The stable event identifier.</summary>
    public long EventId { get; set; }

    /// <summary>The event type name (e.g. "BookAdded", "AnnotationDeleted").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The identifier of the entity this event relates to, when applicable.</summary>
    public string? EntityId { get; set; }

    /// <summary>The type name of the entity, when applicable.</summary>
    public string? EntityType { get; set; }

    /// <summary>The actor identifier, when applicable.</summary>
    public string? ActorId { get; set; }

    /// <summary>JSON snapshot of the entity state before the action.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>JSON snapshot of the entity state after the action.</summary>
    public string? AfterJson { get; set; }

    /// <summary>UTC timestamp when this event was recorded.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>True if this event was generated locally (vs. received from a LAN host).</summary>
    public bool IsLocalOnly { get; set; }
}
