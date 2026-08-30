using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue.Configurations;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// The EF Core <see cref="DbContext"/> for the Ogma Library catalogue of record
/// (ADR-0005). All structured metadata, reading state, annotations, and audit data
/// live in a single SQLite file accessed through this context.
/// </summary>
/// <remarks>
/// <para>
/// EF Core types live only in Infrastructure; the Domain and Application layers
/// never reference <c>Microsoft.EntityFrameworkCore</c> directly (enforced by
/// architecture tests).
/// </para>
/// <para>
/// The context is registered as a transient service so foreground UI services
/// and background workers do not share one EF Core instance. WAL mode and
/// <c>PRAGMA foreign_keys=ON</c> are enabled at connection open.
/// </para>
/// </remarks>
public sealed class CatalogueDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueDbContext"/>.
    /// </summary>
    /// <param name="options">The context options, typically provided by the DI container.</param>
    public CatalogueDbContext(DbContextOptions<CatalogueDbContext> options)
        : base(options)
    {
        EnsureSqlitePragmas();
    }

    // ── Catalogue core ──────────────────────────────────────────────────────────

    /// <summary>Catalogue records for books — the primary identity table.</summary>
    public DbSet<BookRow> Books => Set<BookRow>();

    /// <summary>Physical file records bound to books.</summary>
    public DbSet<BookFileRow> BookFiles => Set<BookFileRow>();

    /// <summary>Field-level metadata with provenance tracking.</summary>
    public DbSet<BookMetadataFieldRow> BookMetadataFields => Set<BookMetadataFieldRow>();

    /// <summary>Normalized author records.</summary>
    public DbSet<AuthorRow> Authors => Set<AuthorRow>();

    /// <summary>Many-to-many join between books and authors.</summary>
    public DbSet<BookAuthorRow> BookAuthors => Set<BookAuthorRow>();

    // ── Shelves ─────────────────────────────────────────────────────────────────

    /// <summary>Virtual and smart shelves.</summary>
    public DbSet<ShelfRow> Shelves => Set<ShelfRow>();

    /// <summary>Many-to-many join between shelves and books.</summary>
    public DbSet<ShelfBookRow> ShelfBooks => Set<ShelfBookRow>();

    // ── Reading state ────────────────────────────────────────────────────────────

    /// <summary>Per-book reading progress (one row per book).</summary>
    public DbSet<ReadingProgressRow> ReadingProgress => Set<ReadingProgressRow>();

    /// <summary>Durable bookmarks with labels.</summary>
    public DbSet<BookmarkRow> Bookmarks => Set<BookmarkRow>();

    /// <summary>Highlights and notes.</summary>
    public DbSet<AnnotationRow> Annotations => Set<AnnotationRow>();

    /// <summary>Annotation bodies (quoted text, note text, geometry).</summary>
    public DbSet<AnnotationBodyRow> AnnotationBodies => Set<AnnotationBodyRow>();

    // ── Search &amp; embeddings ──────────────────────────────────────────────────────

    /// <summary>Per-page extracted text records.</summary>
    public DbSet<ExtractedPageRow> ExtractedPages => Set<ExtractedPageRow>();

    /// <summary>Search-index chunks derived from extracted pages.</summary>
    public DbSet<SearchChunkRow> SearchChunks => Set<SearchChunkRow>();

    /// <summary>Embedding vectors derived from search chunks.</summary>
    public DbSet<EmbeddingVectorRow> EmbeddingVectors => Set<EmbeddingVectorRow>();

    // ── AI &amp; metadata ──────────────────────────────────────────────────────────────

    /// <summary>Local AI query history with soft-delete support.</summary>
    public DbSet<AiQueryHistoryRow> AiQueryHistory => Set<AiQueryHistoryRow>();

    /// <summary>AI consent records by tier/provider/scope.</summary>
    public DbSet<AiConsentRecordRow> AiConsentRecords => Set<AiConsentRecordRow>();

    /// <summary>Immutable AI gateway audit events.</summary>
    public DbSet<AiAuditEventRow> AiAuditEvents => Set<AiAuditEventRow>();

    /// <summary>Metadata lookup results from external providers.</summary>
    public DbSet<MetadataLookupRow> MetadataLookups => Set<MetadataLookupRow>();

    // ── Background jobs &amp; audit ─────────────────────────────────────────────────

    /// <summary>Background job queue with idempotency keys.</summary>
    public DbSet<JobRow> Jobs => Set<JobRow>();

    /// <summary>Append-only local audit trail.</summary>
    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();

    /// <summary>Singleton LAN Host-mode settings.</summary>
    public DbSet<HostModeSettingsRow> HostModeSettings => Set<HostModeSettingsRow>();

    /// <summary>Issued LAN client sessions with hashed bearer tokens.</summary>
    public DbSet<HostClientSessionRow> HostClientSessions => Set<HostClientSessionRow>();

    /// <summary>Classroom-visible library root publishing policies.</summary>
    public DbSet<LibraryPublishSettingsRow> LibraryPublishSettings => Set<LibraryPublishSettingsRow>();

    /// <summary>Administrator-curated classroom shelves.</summary>
    public DbSet<SharedShelfRow> SharedShelves => Set<SharedShelfRow>();

    /// <summary>Book assignments for administrator-curated classroom shelves.</summary>
    public DbSet<SharedShelfBookRow> SharedShelfBooks => Set<SharedShelfBookRow>();

    /// <summary>School-managed classroom profile enrollments.</summary>
    public DbSet<EnrolledProfileRow> EnrolledProfiles => Set<EnrolledProfileRow>();

    /// <summary>Per-profile classroom AI entitlements.</summary>
    public DbSet<SchoolAiEntitlementRow> SchoolAiEntitlements => Set<SchoolAiEntitlementRow>();

    /// <summary>Daily classroom AI usage ledger rows.</summary>
    public DbSet<AiUsageLedgerRow> AiUsageLedger => Set<AiUsageLedgerRow>();

    // ── Phase 09 — Annotations, Layers, Bookmarks, Reading Memory ────────────

    /// <summary>Named annotation layers for grouping highlights and notes.</summary>
    public DbSet<AnnotationLayerRow> AnnotationLayers => Set<AnnotationLayerRow>();

    /// <summary>Extended annotations with normalized region list and layer assignment.</summary>
    public DbSet<AnnotationV2Row> AnnotationsV2 => Set<AnnotationV2Row>();

    /// <summary>Per-book reading-memory journal entries.</summary>
    public DbSet<ReadingMemoryRow> ReadingMemory => Set<ReadingMemoryRow>();

    // ── Work / Edition layer (schema only, Phase 04 WP9) ──────────────────────

    /// <summary>Canonical works (schema-only; UI in Phase 06/07).</summary>
    public DbSet<WorkRow> Works => Set<WorkRow>();

    /// <summary>Published editions of works (schema-only; UI in Phase 06/07).</summary>
    public DbSet<EditionRow> Editions => Set<EditionRow>();

    // Canonical identity persistence (Phase 4 freeze).

    /// <summary>Approved or compatibility library-root identities.</summary>
    public DbSet<LibraryRootRow> LibraryRoots => Set<LibraryRootRow>();

    /// <summary>Exact-byte asset identities.</summary>
    public DbSet<ContentAssetRow> ContentAssets => Set<ContentAssetRow>();

    /// <summary>Physical file occurrences within approved roots.</summary>
    public DbSet<FileOccurrenceRow> FileOccurrences => Set<FileOccurrenceRow>();

    /// <summary>Canonical intellectual works.</summary>
    public DbSet<CanonicalWorkRow> CanonicalWorks => Set<CanonicalWorkRow>();

    /// <summary>Canonical publication editions.</summary>
    public DbSet<CanonicalEditionRow> CanonicalEditions => Set<CanonicalEditionRow>();

    /// <summary>Stable catalogue presentation identities.</summary>
    public DbSet<CatalogueItemRow> CatalogueItems => Set<CatalogueItemRow>();

    /// <summary>Edition-to-content-asset relationships.</summary>
    public DbSet<EditionContentAssetRow> EditionContentAssets => Set<EditionContentAssetRow>();

    /// <summary>Catalogue-item-to-occurrence relationships.</summary>
    public DbSet<CatalogueItemOccurrenceRow> CatalogueItemOccurrences => Set<CatalogueItemOccurrenceRow>();

    /// <summary>Source-scoped canonical bibliographic identifiers.</summary>
    public DbSet<BibliographicIdentifierRow> BibliographicIdentifiers => Set<BibliographicIdentifierRow>();

    /// <summary>Versioned path-free identity decisions.</summary>
    public DbSet<IdentityDecisionRow> IdentityDecisions => Set<IdentityDecisionRow>();

    /// <summary>Aliases from legacy BookIds to canonical identities.</summary>
    public DbSet<LegacyIdentityAliasRow> LegacyIdentityAliases => Set<LegacyIdentityAliasRow>();

    /// <summary>Durable root scan sessions.</summary>
    public DbSet<ScanSessionRow> ScanSessions => Set<ScanSessionRow>();

    /// <summary>Durable leased processing stages.</summary>
    public DbSet<StageExecutionRow> StageExecutions => Set<StageExecutionRow>();

    /// <summary>Latest root-relative file observations.</summary>
    public DbSet<DiscoveryObservationRow> DiscoveryObservations => Set<DiscoveryObservationRow>();

    /// <summary>Durable directory discovery checkpoints.</summary>
    public DbSet<DirectoryCheckpointRow> DirectoryCheckpoints => Set<DirectoryCheckpointRow>();

    // ── Configuration ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new BookConfiguration());
        modelBuilder.ApplyConfiguration(new BookFileConfiguration());
        modelBuilder.ApplyConfiguration(new BookMetadataFieldConfiguration());
        modelBuilder.ApplyConfiguration(new AuthorConfiguration());
        modelBuilder.ApplyConfiguration(new BookAuthorConfiguration());
        modelBuilder.ApplyConfiguration(new ShelfConfiguration());
        modelBuilder.ApplyConfiguration(new ShelfBookConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingProgressConfiguration());
        modelBuilder.ApplyConfiguration(new BookmarkConfiguration());
        modelBuilder.ApplyConfiguration(new AnnotationConfiguration());
        modelBuilder.ApplyConfiguration(new AnnotationBodyConfiguration());
        modelBuilder.ApplyConfiguration(new ExtractedPageConfiguration());
        modelBuilder.ApplyConfiguration(new SearchChunkConfiguration());
        modelBuilder.ApplyConfiguration(new EmbeddingVectorConfiguration());
        modelBuilder.ApplyConfiguration(new AiQueryHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AiConsentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AiAuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new MetadataLookupConfiguration());
        modelBuilder.ApplyConfiguration(new JobConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new HostModeSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new HostClientSessionConfiguration());
        modelBuilder.ApplyConfiguration(new LibraryPublishSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new SharedShelfConfiguration());
        modelBuilder.ApplyConfiguration(new SharedShelfBookConfiguration());
        modelBuilder.ApplyConfiguration(new EnrolledProfileConfiguration());
        modelBuilder.ApplyConfiguration(new SchoolAiEntitlementConfiguration());
        modelBuilder.ApplyConfiguration(new AiUsageLedgerConfiguration());
        modelBuilder.ApplyConfiguration(new WorkConfiguration());
        modelBuilder.ApplyConfiguration(new EditionConfiguration());
        CanonicalIdentityConfiguration.Configure(modelBuilder);
        modelBuilder.ApplyConfiguration(new ScanSessionConfiguration());
        modelBuilder.ApplyConfiguration(new StageExecutionConfiguration());
        modelBuilder.ApplyConfiguration(new DiscoveryObservationConfiguration());
        modelBuilder.ApplyConfiguration(new DirectoryCheckpointConfiguration());

        // Phase 09 — Annotations, Layers, Reading Memory.
        modelBuilder.ApplyConfiguration(new AnnotationLayerConfiguration());
        modelBuilder.ApplyConfiguration(new AnnotationV2Configuration());
        modelBuilder.ApplyConfiguration(new ReadingMemoryConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (!optionsBuilder.IsConfigured)
        {
            // Fallback used only by design-time tools via the factory.
            optionsBuilder.UseSqlite("Data Source=:memory:");
        }

        base.OnConfiguring(optionsBuilder);
    }

    private void EnsureSqlitePragmas()
    {
        if (Database.GetDbConnection() is not SqliteConnection connection)
        {
            return;
        }

        Database.OpenConnection();
        ExecutePragma(connection, "PRAGMA foreign_keys=ON;");

        if (!string.Equals(connection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            ExecutePragma(connection, "PRAGMA journal_mode=WAL;");
        }
    }

    private static void ExecutePragma(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
