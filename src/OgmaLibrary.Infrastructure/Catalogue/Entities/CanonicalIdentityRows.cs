namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Persistence row for one approved library-root identity.</summary>
public sealed class LibraryRootRow
{
    /// <summary>The 26-character canonical root identifier.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>A non-secret display label.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Canonical absolute locator, or null for a legacy compatibility root.</summary>
    public string? CanonicalLocator { get; set; }

    /// <summary>Opaque volume or mount hint used during relink suggestions.</summary>
    public string? VolumeIdentity { get; set; }

    /// <summary>Root health state; detailed path authority is introduced in Phase 5.</summary>
    public int RootStatus { get; set; }

    /// <summary>Permission probe state.</summary>
    public int PermissionStatus { get; set; }

    /// <summary>Whether this root was created solely to migrate legacy rows.</summary>
    public bool IsCompatibilityRoot { get; set; }

    /// <summary>Whether this root participates in future scans.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether traversal through symbolic links was explicitly approved.</summary>
    public bool AllowSymlinkTraversal { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC time of the latest bounded health probe.</summary>
    public DateTimeOffset? LastHealthCheckUtc { get; set; }

    /// <summary>UTC time of the latest successful scan.</summary>
    public DateTimeOffset? LastSuccessfulScanUtc { get; set; }
}

/// <summary>Persistence row for one exact source-file byte identity.</summary>
public sealed class ContentAssetRow
{
    /// <summary>The 26-character canonical asset identifier.</summary>
    public string ContentAssetId { get; set; } = string.Empty;

    /// <summary>The complete-file SHA-256 digest.</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>The version of the fingerprint contract.</summary>
    public int FingerprintVersion { get; set; }

    /// <summary>Byte size when the legacy record supplied a valid positive value.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Zero for migrated/unverified evidence; one after recomputation.</summary>
    public int VerificationStatus { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>Persistence row for one physical occurrence within one root.</summary>
public sealed class FileOccurrenceRow
{
    /// <summary>The 26-character occurrence identifier.</summary>
    public string FileOccurrenceId { get; set; } = string.Empty;

    /// <summary>The owning root identity.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>The exact-byte asset identity, or null while unknown.</summary>
    public string? ContentAssetId { get; set; }

    /// <summary>The original root-relative locator.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>NFC, slash-normalized locator used by the Phase 4 uniqueness rule.</summary>
    public string NormalizedRelativePath { get; set; } = string.Empty;

    /// <summary>Availability state, independent of deletion intent.</summary>
    public int AvailabilityStatus { get; set; }

    /// <summary>Observed byte size, when known.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Observed UTC mtime ticks, when known.</summary>
    public long? ModifiedUtcTicks { get; set; }

    /// <summary>Optional legacy structural fingerprint.</summary>
    public string? PdfFingerprint { get; set; }

    /// <summary>UTC time the occurrence was last observed.</summary>
    public DateTimeOffset? LastSeenUtc { get; set; }

    /// <summary>UTC time the occurrence was first absent after a healthy scan.</summary>
    public DateTimeOffset? MissingSinceUtc { get; set; }
}

/// <summary>Persistence row for an intellectual work in the canonical model.</summary>
public sealed class CanonicalWorkRow
{
    /// <summary>The 26-character work identifier.</summary>
    public string WorkId { get; set; } = string.Empty;

    /// <summary>Unknown, provisional, identified or ambiguous.</summary>
    public int ResolutionState { get; set; }

    /// <summary>Compatibility title copied without changing legacy metadata.</summary>
    public string? CanonicalTitle { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>Persistence row for a publication in the canonical model.</summary>
public sealed class CanonicalEditionRow
{
    /// <summary>The 26-character edition identifier.</summary>
    public string EditionId { get; set; } = string.Empty;

    /// <summary>The parent work identifier.</summary>
    public string WorkId { get; set; } = string.Empty;

    /// <summary>Unknown, provisional, identified or ambiguous.</summary>
    public int ResolutionState { get; set; }

    /// <summary>Language code, when already known.</summary>
    public string? Language { get; set; }

    /// <summary>Publication year, when already known.</summary>
    public int? PublicationYear { get; set; }

    /// <summary>Publisher, when already known.</summary>
    public string? Publisher { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>Stable identity consumed by catalogue presentation clients.</summary>
public sealed class CatalogueItemRow
{
    /// <summary>The 26-character catalogue item identifier.</summary>
    public string CatalogueItemId { get; set; } = string.Empty;

    /// <summary>The selected work identifier.</summary>
    public string WorkId { get; set; } = string.Empty;

    /// <summary>The selected edition identifier.</summary>
    public string EditionId { get; set; } = string.Empty;

    /// <summary>The preferred occurrence, or null when none is usable.</summary>
    public string? PreferredOccurrenceId { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>Many-to-many relationship between editions and exact assets.</summary>
public sealed class EditionContentAssetRow
{
    /// <summary>The edition identifier.</summary>
    public string EditionId { get; set; } = string.Empty;

    /// <summary>The content asset identifier.</summary>
    public string ContentAssetId { get; set; } = string.Empty;
}

/// <summary>Relationship preserving catalogue ownership even before hashing.</summary>
public sealed class CatalogueItemOccurrenceRow
{
    /// <summary>The catalogue item identifier.</summary>
    public string CatalogueItemId { get; set; } = string.Empty;

    /// <summary>The file occurrence identifier.</summary>
    public string FileOccurrenceId { get; set; } = string.Empty;
}

/// <summary>Source-attributed external identifier for a work or edition.</summary>
public sealed class BibliographicIdentifierRow
{
    /// <summary>Database identity.</summary>
    public long BibliographicIdentifierId { get; set; }

    /// <summary>Zero for work and one for edition.</summary>
    public int OwnerScope { get; set; }

    /// <summary>Work owner when <see cref="OwnerScope"/> is zero.</summary>
    public string? WorkId { get; set; }

    /// <summary>Edition owner when <see cref="OwnerScope"/> is one.</summary>
    public string? EditionId { get; set; }

    /// <summary>Provider or standard namespace.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Identifier kind from the canonical domain enum.</summary>
    public int IdentifierKind { get; set; }

    /// <summary>Normalized provider/standard value.</summary>
    public string NormalizedValue { get; set; } = string.Empty;
}

/// <summary>Versioned, path-free identity decision.</summary>
public sealed class IdentityDecisionRow
{
    /// <summary>The 26-character decision identifier.</summary>
    public string IdentityDecisionId { get; set; } = string.Empty;

    /// <summary>The subject occurrence.</summary>
    public string SubjectOccurrenceId { get; set; } = string.Empty;

    /// <summary>The candidate occurrence.</summary>
    public string CandidateOccurrenceId { get; set; } = string.Empty;

    /// <summary>Canonical relationship enum value.</summary>
    public int Relationship { get; set; }

    /// <summary>Automatic or review-required disposition.</summary>
    public int Disposition { get; set; }

    /// <summary>Highest evidence tier used.</summary>
    public int EvidenceTier { get; set; }

    /// <summary>Normalized confidence in [0, 1].</summary>
    public double Confidence { get; set; }

    /// <summary>Identity policy version.</summary>
    public int PolicyVersion { get; set; }

    /// <summary>UTC decision time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>Compatibility alias from a legacy BookId to canonical identities.</summary>
public sealed class LegacyIdentityAliasRow
{
    /// <summary>The legacy catalogue BookId.</summary>
    public string LegacyBookId { get; set; } = string.Empty;

    /// <summary>The canonical catalogue item.</summary>
    public string CatalogueItemId { get; set; } = string.Empty;

    /// <summary>The canonical work.</summary>
    public string WorkId { get; set; } = string.Empty;

    /// <summary>The canonical edition.</summary>
    public string EditionId { get; set; } = string.Empty;

    /// <summary>The migration contract version that created this alias.</summary>
    public int MigrationVersion { get; set; }

    /// <summary>UTC alias creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
