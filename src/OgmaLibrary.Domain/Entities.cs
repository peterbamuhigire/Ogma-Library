namespace OgmaLibrary.Domain;

/// <summary>
/// Compatibility shape for one pre-canonical <c>Books</c> row. It is not a work,
/// edition, content asset, or file identity and is removed by the Phase 4 migration.
/// </summary>
public sealed class LegacyCatalogueRecord
{
    /// <summary>The stable identity of this book.</summary>
    public required BookId Id { get; init; }

    /// <summary>The bibliographic title, or <see langword="null"/> until enriched.</summary>
    public string? Title { get; set; }

    /// <summary>The detected or enriched ISBN, when known.</summary>
    public Isbn? Isbn { get; set; }

    /// <summary>The publication year, when known.</summary>
    public int? Year { get; set; }

    /// <summary>The reader's rating from 1 to 5, when set.</summary>
    public int? Rating { get; set; }

    /// <summary>The physical files bound to this book.</summary>
    public IReadOnlyList<LegacyFileRecord> Files { get; init; } = [];

    /// <summary>The authors associated with this book.</summary>
    public IReadOnlyList<Author> Authors { get; init; } = [];

    /// <summary>The virtual shelves this book belongs to (FR-CAT-003).</summary>
    public IReadOnlyList<Shelf> Shelves { get; init; } = [];

    /// <summary>The free-form tags applied to this book.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// Compatibility file projection over the pre-canonical schema. Unknown facts
/// remain null; they are never synthesized from a path.
/// </summary>
public sealed class LegacyFileRecord
{
    /// <summary>The path relative to the library root — the primary locator.</summary>
    public required string RelativePath { get; init; }

    /// <summary>The verified SHA-256 content hash, or null when legacy identity is unknown.</summary>
    public ContentHash? ContentHash { get; init; }

    /// <summary>The file size in bytes, used by the incremental rescan.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>The last-modified timestamp (UTC), used by the incremental rescan.</summary>
    public DateTimeOffset? ModifiedUtc { get; init; }

    /// <summary>An optional structural PDF fingerprint, a secondary content signal.</summary>
    public string? PdfFingerprint { get; set; }

    /// <summary>Whether the file is currently present and readable (FR-LIB-004).</summary>
    public AvailabilityStatus Availability { get; set; } = AvailabilityStatus.Available;
}

/// <summary>A distinct author, normalized for index-backed browsing (FR-CAT-002).</summary>
public sealed class Author
{
    /// <summary>The author's display name.</summary>
    public required string Name { get; init; }

    /// <summary>The normalized form used for matching and sorting.</summary>
    public required string NormalizedName { get; init; }
}

/// <summary>
/// A virtual shelf. A book may belong to many shelves independently of its file
/// path (FR-CAT-003). A smart shelf is a shelf whose membership is defined by a
/// saved filter rather than explicit assignment.
/// </summary>
public sealed class Shelf
{
    /// <summary>The shelf's stable identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The shelf's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is a smart shelf (filter-defined) rather than manual.</summary>
    public bool IsSmart { get; init; }
}

/// <summary>
/// The last reading position and status for a catalogue presentation (FR-READ-001).
/// </summary>
public sealed class ReadingProgress
{
    /// <summary>The zero-based index of the last page read.</summary>
    public required int LastPageIndex { get; set; }

    /// <summary>The vertical scroll offset within the last page, in [0.0, 1.0].</summary>
    public required double LastScrollOffset { get; set; }

    /// <summary>The reading status of the book.</summary>
    public ReadingStatus Status { get; set; } = ReadingStatus.Unread;

    /// <summary>The UTC time the book was last opened.</summary>
    public DateTimeOffset? LastOpenedUtc { get; set; }
}

/// <summary>
/// A highlight or note anchored to a region within a selected content asset,
/// persisted in the catalogue of record before being reported saved (NFR-OGMA-008).
/// </summary>
public sealed class Annotation
{
    /// <summary>The annotation's stable identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The kind of annotation.</summary>
    public required AnnotationKind Kind { get; init; }

    /// <summary>The zero-based page index the annotation anchors to.</summary>
    public required int PageIndex { get; init; }

    /// <summary>The normalized [0,1] start point of the selection region.</summary>
    public required (double X, double Y) SelectionStart { get; init; }

    /// <summary>The normalized [0,1] end point of the selection region.</summary>
    public required (double X, double Y) SelectionEnd { get; init; }

    /// <summary>The highlight colour as a hex string, when the kind is a highlight.</summary>
    public string? HighlightColor { get; set; }

    /// <summary>The note text, when the kind is a note.</summary>
    public string? NoteText { get; set; }
}

/// <summary>
/// An append-only entry in the local, tamper-evident audit trail recorded for every
/// sensitive action (NFR-PROD-013, CTRL-OGMA-018). Audit events are never deleted.
/// </summary>
public sealed class AuditEvent
{
    /// <summary>The audit event's stable identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The action that was recorded (e.g. credential-change, off-device-call).</summary>
    public required string EventType { get; init; }

    /// <summary>The identifier of the entity the action affected, when applicable.</summary>
    public string? EntityId { get; init; }

    /// <summary>The identifier of the actor that performed the action, when applicable.</summary>
    public string? ActorId { get; init; }

    /// <summary>The UTC time the action occurred.</summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>A JSON payload describing the action's scope and the active privacy tier.</summary>
    public string? Payload { get; init; }
}
