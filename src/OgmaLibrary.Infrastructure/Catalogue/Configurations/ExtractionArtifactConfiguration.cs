using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for versioned extraction artifacts.</summary>
public sealed class ExtractionArtifactConfiguration : IEntityTypeConfiguration<ExtractionArtifactRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExtractionArtifactRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ExtractionArtifacts", table =>
        {
            table.HasCheckConstraint("CK_ExtractionArtifacts_Status", "Status BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_ExtractionArtifacts_Pages", "PagesProcessed >= 0 AND FailedPages >= 0");
            table.HasCheckConstraint("CK_ExtractionArtifacts_Manifest", "ManifestHash IS NULL OR length(ManifestHash) = 64");
            table.HasCheckConstraint("CK_ExtractionArtifacts_Toc", "TocEntries >= 0 AND TocQuality BETWEEN 0 AND 3");
        });
        builder.HasKey(row => row.ExtractionArtifactId);
        builder.Property(row => row.ExtractionArtifactId).ValueGeneratedOnAdd();
        builder.Property(row => row.BookId).IsRequired().HasMaxLength(128);
        builder.Property(row => row.ContentHash).HasMaxLength(64);
        builder.Property(row => row.ExtractorVersion).IsRequired().HasMaxLength(128);
        builder.Property(row => row.Status).HasDefaultValue(0);
        builder.Property(row => row.ManifestHash).HasMaxLength(64);
        builder.Property(row => row.TocEntries).HasDefaultValue(0);
        builder.Property(row => row.TocQuality).HasDefaultValue(0);
        builder.HasIndex(row => new { row.BookId, row.ContentHash, row.ExtractorVersion })
            .IsUnique()
            .HasDatabaseName("UX_ExtractionArtifacts_Book_Content_Version");
        builder.HasOne<BookRow>()
            .WithMany()
            .HasForeignKey(row => row.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
