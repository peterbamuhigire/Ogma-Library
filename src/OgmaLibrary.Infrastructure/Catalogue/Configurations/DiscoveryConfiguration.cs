using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for incremental discovery observations and checkpoints.</summary>
public sealed class DiscoveryObservationConfiguration : IEntityTypeConfiguration<DiscoveryObservationRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DiscoveryObservationRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DiscoveryObservations", table =>
        {
            table.HasCheckConstraint("CK_DiscoveryObservations_RootId", "length(LibraryRootId) = 26");
            table.HasCheckConstraint("CK_DiscoveryObservations_Path", "length(NormalizedRelativePath) > 0");
            table.HasCheckConstraint("CK_DiscoveryObservations_Size", "SizeBytes >= 0");
        });
        builder.HasKey(row => row.DiscoveryObservationId);
        builder.Property(row => row.DiscoveryObservationId).ValueGeneratedOnAdd();
        builder.Property(row => row.LibraryRootId).IsRequired().HasMaxLength(26);
        builder.Property(row => row.NormalizedRelativePath).IsRequired().HasMaxLength(4096);
        builder.Property(row => row.LastObservedScanSessionId);
        builder.Property(row => row.Sha256Hash).HasMaxLength(64);
        builder.HasIndex(row => new { row.LibraryRootId, row.NormalizedRelativePath })
            .IsUnique()
            .HasDatabaseName("UX_DiscoveryObservations_Root_Path");
        builder.HasOne<LibraryRootRow>()
            .WithMany()
            .HasForeignKey(row => row.LibraryRootId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF constraints for directory scan checkpoints.</summary>
public sealed class DirectoryCheckpointConfiguration : IEntityTypeConfiguration<DirectoryCheckpointRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DirectoryCheckpointRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DirectoryCheckpoints", table =>
        {
            table.HasCheckConstraint("CK_DirectoryCheckpoints_RootId", "length(LibraryRootId) = 26");
            table.HasCheckConstraint("CK_DirectoryCheckpoints_Count", "LastObservedFileCount >= 0");
        });
        builder.HasKey(row => row.DirectoryCheckpointId);
        builder.Property(row => row.DirectoryCheckpointId).ValueGeneratedOnAdd();
        builder.Property(row => row.LibraryRootId).IsRequired().HasMaxLength(26);
        builder.Property(row => row.NormalizedRelativeDirectory).IsRequired().HasMaxLength(4096);
        builder.Property(row => row.LastScanSessionId);
        builder.Property(row => row.ScanState).IsRequired();
        builder.Property(row => row.ResumeCursorRelativeDirectory).HasMaxLength(4096);
        builder.Property(row => row.LastErrorCode).HasMaxLength(128);
        builder.HasIndex(row => new { row.LibraryRootId, row.NormalizedRelativeDirectory })
            .IsUnique()
            .HasDatabaseName("UX_DirectoryCheckpoints_Root_Directory");
        builder.HasOne<LibraryRootRow>()
            .WithMany()
            .HasForeignKey(row => row.LibraryRootId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
