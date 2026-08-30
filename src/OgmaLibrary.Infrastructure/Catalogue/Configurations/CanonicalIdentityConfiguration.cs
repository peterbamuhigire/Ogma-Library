using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

internal static class CanonicalIdentityConfiguration
{
    private const int CanonicalIdLength = 26;

    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureLibraryRoot(modelBuilder);
        ConfigureContentAsset(modelBuilder);
        ConfigureFileOccurrence(modelBuilder);
        ConfigureWorkAndEdition(modelBuilder);
        ConfigureCatalogueItem(modelBuilder);
        ConfigureJoins(modelBuilder);
        ConfigureBibliographicIdentifier(modelBuilder);
        ConfigureIdentityDecision(modelBuilder);
        ConfigureLegacyAlias(modelBuilder);
    }

    private static void ConfigureLibraryRoot(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<LibraryRootRow>();
        builder.ToTable("LibraryRoots", table =>
        {
            table.HasCheckConstraint("CK_LibraryRoots_Id", "length(LibraryRootId) = 26");
            table.HasCheckConstraint("CK_LibraryRoots_Status", "RootStatus BETWEEN 0 AND 3");
        });
        builder.HasKey(row => row.LibraryRootId);
        builder.Property(row => row.LibraryRootId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.DisplayName).IsRequired().HasMaxLength(256);
        builder.Property(row => row.CanonicalLocator).HasMaxLength(4096);
        builder.Property(row => row.VolumeIdentity).HasMaxLength(512);
        builder.Property(row => row.RootStatus);
        builder.Property(row => row.PermissionStatus);
        builder.Property(row => row.IsCompatibilityRoot);
        builder.Property(row => row.IsEnabled).HasDefaultValue(true);
        builder.Property(row => row.AllowSymlinkTraversal).HasDefaultValue(false);
        builder.Property(row => row.CreatedUtc);
        builder.Property(row => row.LastHealthCheckUtc);
        builder.Property(row => row.LastSuccessfulScanUtc);
        builder.HasIndex(row => row.CanonicalLocator)
            .IsUnique()
            .HasDatabaseName("UX_LibraryRoots_CanonicalLocator");
    }

    private static void ConfigureContentAsset(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ContentAssetRow>();
        builder.ToTable("ContentAssets", table =>
        {
            table.HasCheckConstraint("CK_ContentAssets_Id", "length(ContentAssetId) = 26");
            table.HasCheckConstraint(
                "CK_ContentAssets_Sha256",
                "length(Sha256Hash) = 64 AND Sha256Hash NOT GLOB '*[^0-9a-f]*'");
            table.HasCheckConstraint("CK_ContentAssets_FingerprintVersion", "FingerprintVersion > 0");
            table.HasCheckConstraint("CK_ContentAssets_Size", "SizeBytes IS NULL OR SizeBytes > 0");
            table.HasCheckConstraint("CK_ContentAssets_Verification", "VerificationStatus IN (0, 1)");
        });
        builder.HasKey(row => row.ContentAssetId);
        builder.Property(row => row.ContentAssetId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.Sha256Hash).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(row => row.FingerprintVersion);
        builder.Property(row => row.SizeBytes);
        builder.Property(row => row.VerificationStatus);
        builder.Property(row => row.CreatedUtc);
        builder.HasIndex(row => new { row.Sha256Hash, row.FingerprintVersion })
            .IsUnique()
            .HasDatabaseName("UX_ContentAssets_Hash_Version");
    }

    private static void ConfigureFileOccurrence(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<FileOccurrenceRow>();
        builder.ToTable("FileOccurrences", table =>
        {
            table.HasCheckConstraint("CK_FileOccurrences_Id", "length(FileOccurrenceId) = 26");
            table.HasCheckConstraint("CK_FileOccurrences_RootId", "length(LibraryRootId) = 26");
            table.HasCheckConstraint(
                "CK_FileOccurrences_AssetId",
                "ContentAssetId IS NULL OR length(ContentAssetId) = 26");
            table.HasCheckConstraint("CK_FileOccurrences_Availability", "AvailabilityStatus IN (0, 1)");
            table.HasCheckConstraint("CK_FileOccurrences_Size", "SizeBytes IS NULL OR SizeBytes >= 0");
        });
        builder.HasKey(row => row.FileOccurrenceId);
        builder.Property(row => row.FileOccurrenceId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.LibraryRootId).IsRequired().HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.ContentAssetId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.RelativePath).IsRequired().HasMaxLength(4096);
        builder.Property(row => row.NormalizedRelativePath).IsRequired().HasMaxLength(4096);
        builder.Property(row => row.PdfFingerprint).HasMaxLength(512);
        builder.HasIndex(row => new { row.LibraryRootId, row.NormalizedRelativePath })
            .IsUnique()
            .HasDatabaseName("UX_FileOccurrences_Root_NormalizedPath");
        builder.HasIndex(row => new { row.ContentAssetId, row.AvailabilityStatus })
            .HasDatabaseName("IX_FileOccurrences_Asset_Availability");
        builder.HasOne<LibraryRootRow>()
            .WithMany()
            .HasForeignKey(row => row.LibraryRootId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContentAssetRow>()
            .WithMany()
            .HasForeignKey(row => row.ContentAssetId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureWorkAndEdition(ModelBuilder modelBuilder)
    {
        var work = modelBuilder.Entity<CanonicalWorkRow>();
        work.ToTable("CanonicalWorks", table =>
        {
            table.HasCheckConstraint("CK_CanonicalWorks_Id", "length(WorkId) = 26");
            table.HasCheckConstraint("CK_CanonicalWorks_State", "ResolutionState BETWEEN 0 AND 3");
        });
        work.HasKey(row => row.WorkId);
        work.Property(row => row.WorkId).HasMaxLength(CanonicalIdLength);
        work.Property(row => row.CanonicalTitle).HasMaxLength(1024);
        work.HasIndex(row => row.ResolutionState).HasDatabaseName("IX_CanonicalWorks_State");

        var edition = modelBuilder.Entity<CanonicalEditionRow>();
        edition.ToTable("CanonicalEditions", table =>
        {
            table.HasCheckConstraint("CK_CanonicalEditions_Id", "length(EditionId) = 26");
            table.HasCheckConstraint("CK_CanonicalEditions_WorkId", "length(WorkId) = 26");
            table.HasCheckConstraint("CK_CanonicalEditions_State", "ResolutionState BETWEEN 0 AND 3");
        });
        edition.HasKey(row => row.EditionId);
        edition.HasAlternateKey(row => new { row.EditionId, row.WorkId });
        edition.Property(row => row.EditionId).HasMaxLength(CanonicalIdLength);
        edition.Property(row => row.WorkId).IsRequired().HasMaxLength(CanonicalIdLength);
        edition.Property(row => row.Language).HasMaxLength(16);
        edition.Property(row => row.Publisher).HasMaxLength(512);
        edition.HasIndex(row => new { row.WorkId, row.ResolutionState })
            .HasDatabaseName("IX_CanonicalEditions_Work_State");
        edition.HasOne<CanonicalWorkRow>()
            .WithMany()
            .HasForeignKey(row => row.WorkId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCatalogueItem(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CatalogueItemRow>();
        builder.ToTable("CatalogueItems", table =>
        {
            table.HasCheckConstraint("CK_CatalogueItems_Id", "length(CatalogueItemId) = 26");
            table.HasCheckConstraint("CK_CatalogueItems_WorkId", "length(WorkId) = 26");
            table.HasCheckConstraint("CK_CatalogueItems_EditionId", "length(EditionId) = 26");
            table.HasCheckConstraint(
                "CK_CatalogueItems_PreferredOccurrenceId",
                "PreferredOccurrenceId IS NULL OR length(PreferredOccurrenceId) = 26");
        });
        builder.HasKey(row => row.CatalogueItemId);
        builder.Property(row => row.CatalogueItemId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.WorkId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.EditionId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.PreferredOccurrenceId).HasMaxLength(CanonicalIdLength);
        builder.HasIndex(row => new { row.WorkId, row.EditionId })
            .HasDatabaseName("IX_CatalogueItems_Work_Edition");
        builder.HasOne<CanonicalEditionRow>()
            .WithMany()
            .HasForeignKey(row => new { row.EditionId, row.WorkId })
            .HasPrincipalKey(row => new { row.EditionId, row.WorkId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileOccurrenceRow>()
            .WithMany()
            .HasForeignKey(row => row.PreferredOccurrenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureJoins(ModelBuilder modelBuilder)
    {
        var editionAsset = modelBuilder.Entity<EditionContentAssetRow>();
        editionAsset.ToTable("EditionContentAssets");
        editionAsset.HasKey(row => new { row.EditionId, row.ContentAssetId });
        editionAsset.Property(row => row.EditionId).HasMaxLength(CanonicalIdLength);
        editionAsset.Property(row => row.ContentAssetId).HasMaxLength(CanonicalIdLength);
        editionAsset.HasIndex(row => row.ContentAssetId)
            .HasDatabaseName("IX_EditionContentAssets_AssetId");
        editionAsset.HasOne<CanonicalEditionRow>()
            .WithMany()
            .HasForeignKey(row => row.EditionId)
            .OnDelete(DeleteBehavior.Cascade);
        editionAsset.HasOne<ContentAssetRow>()
            .WithMany()
            .HasForeignKey(row => row.ContentAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        var itemOccurrence = modelBuilder.Entity<CatalogueItemOccurrenceRow>();
        itemOccurrence.ToTable("CatalogueItemOccurrences");
        itemOccurrence.HasKey(row => new { row.CatalogueItemId, row.FileOccurrenceId });
        itemOccurrence.Property(row => row.CatalogueItemId).HasMaxLength(CanonicalIdLength);
        itemOccurrence.Property(row => row.FileOccurrenceId).HasMaxLength(CanonicalIdLength);
        itemOccurrence.HasIndex(row => row.FileOccurrenceId)
            .HasDatabaseName("IX_CatalogueItemOccurrences_OccurrenceId");
        itemOccurrence.HasOne<CatalogueItemRow>()
            .WithMany()
            .HasForeignKey(row => row.CatalogueItemId)
            .OnDelete(DeleteBehavior.Cascade);
        itemOccurrence.HasOne<FileOccurrenceRow>()
            .WithMany()
            .HasForeignKey(row => row.FileOccurrenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureBibliographicIdentifier(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<BibliographicIdentifierRow>();
        builder.ToTable("BibliographicIdentifiers", table =>
        {
            table.HasCheckConstraint("CK_BibliographicIdentifiers_Scope", "OwnerScope IN (0, 1)");
            table.HasCheckConstraint("CK_BibliographicIdentifiers_Kind", "IdentifierKind BETWEEN 0 AND 4");
            table.HasCheckConstraint(
                "CK_BibliographicIdentifiers_Owner",
                "(OwnerScope = 0 AND WorkId IS NOT NULL AND EditionId IS NULL) OR " +
                "(OwnerScope = 1 AND EditionId IS NOT NULL AND WorkId IS NULL)");
        });
        builder.HasKey(row => row.BibliographicIdentifierId);
        builder.Property(row => row.BibliographicIdentifierId).ValueGeneratedOnAdd();
        builder.Property(row => row.WorkId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.EditionId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.Source).IsRequired().HasMaxLength(128);
        builder.Property(row => row.NormalizedValue).IsRequired().HasMaxLength(512);
        builder.HasIndex(row => new
            {
                row.OwnerScope,
                row.Source,
                row.IdentifierKind,
                row.NormalizedValue,
            })
            .IsUnique()
            .HasDatabaseName("UX_BibliographicIdentifiers_ScopedValue");
        builder.HasIndex(row => row.WorkId).HasDatabaseName("IX_BibliographicIdentifiers_WorkId");
        builder.HasIndex(row => row.EditionId).HasDatabaseName("IX_BibliographicIdentifiers_EditionId");
        builder.HasOne<CanonicalWorkRow>()
            .WithMany()
            .HasForeignKey(row => row.WorkId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CanonicalEditionRow>()
            .WithMany()
            .HasForeignKey(row => row.EditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureIdentityDecision(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<IdentityDecisionRow>();
        builder.ToTable("IdentityDecisions", table =>
        {
            table.HasCheckConstraint("CK_IdentityDecisions_Id", "length(IdentityDecisionId) = 26");
            table.HasCheckConstraint(
                "CK_IdentityDecisions_DistinctOccurrences",
                "SubjectOccurrenceId <> CandidateOccurrenceId");
            table.HasCheckConstraint("CK_IdentityDecisions_Relationship", "Relationship BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_IdentityDecisions_Disposition", "Disposition IN (0, 1)");
            table.HasCheckConstraint("CK_IdentityDecisions_Tier", "EvidenceTier BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_IdentityDecisions_Confidence", "Confidence BETWEEN 0.0 AND 1.0");
            table.HasCheckConstraint("CK_IdentityDecisions_PolicyVersion", "PolicyVersion > 0");
        });
        builder.HasKey(row => row.IdentityDecisionId);
        builder.Property(row => row.IdentityDecisionId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.SubjectOccurrenceId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.CandidateOccurrenceId).HasMaxLength(CanonicalIdLength);
        builder.HasIndex(row => new { row.SubjectOccurrenceId, row.CandidateOccurrenceId, row.PolicyVersion })
            .HasDatabaseName("IX_IdentityDecisions_Pair_Version");
        builder.HasOne<FileOccurrenceRow>()
            .WithMany()
            .HasForeignKey(row => row.SubjectOccurrenceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileOccurrenceRow>()
            .WithMany()
            .HasForeignKey(row => row.CandidateOccurrenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureLegacyAlias(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<LegacyIdentityAliasRow>();
        builder.ToTable("LegacyIdentityAliases", table =>
        {
            table.HasCheckConstraint("CK_LegacyIdentityAliases_CatalogueItemId", "length(CatalogueItemId) = 26");
            table.HasCheckConstraint("CK_LegacyIdentityAliases_WorkId", "length(WorkId) = 26");
            table.HasCheckConstraint("CK_LegacyIdentityAliases_EditionId", "length(EditionId) = 26");
            table.HasCheckConstraint("CK_LegacyIdentityAliases_Version", "MigrationVersion > 0");
        });
        builder.HasKey(row => row.LegacyBookId);
        builder.Property(row => row.LegacyBookId).HasMaxLength(128);
        builder.Property(row => row.CatalogueItemId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.WorkId).HasMaxLength(CanonicalIdLength);
        builder.Property(row => row.EditionId).HasMaxLength(CanonicalIdLength);
        builder.HasIndex(row => row.CatalogueItemId).IsUnique()
            .HasDatabaseName("UX_LegacyIdentityAliases_CatalogueItemId");
        builder.HasOne<CatalogueItemRow>()
            .WithMany()
            .HasForeignKey(row => row.CatalogueItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CanonicalEditionRow>()
            .WithMany()
            .HasForeignKey(row => new { row.EditionId, row.WorkId })
            .HasPrincipalKey(row => new { row.EditionId, row.WorkId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
