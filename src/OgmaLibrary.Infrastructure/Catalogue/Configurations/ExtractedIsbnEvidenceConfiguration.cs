using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for extracted ISBN evidence.</summary>
public sealed class ExtractedIsbnEvidenceConfiguration : IEntityTypeConfiguration<ExtractedIsbnEvidenceRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExtractedIsbnEvidenceRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ExtractedIsbnEvidence", table =>
        {
            table.HasCheckConstraint("CK_ExtractedIsbnEvidence_Kind", "IdentifierKind IN (0, 1)");
            table.HasCheckConstraint("CK_ExtractedIsbnEvidence_Source", "Source BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_ExtractedIsbnEvidence_Rank", "Rank >= 0");
            table.HasCheckConstraint("CK_ExtractedIsbnEvidence_Isbn", "length(IsbnNormalized) IN (10, 13)");
        });
        builder.HasKey(row => row.ExtractedIsbnEvidenceId);
        builder.Property(row => row.ExtractedIsbnEvidenceId).ValueGeneratedOnAdd();
        builder.Property(row => row.BookId).IsRequired().HasMaxLength(128);
        builder.Property(row => row.ExtractionArtifactId);
        builder.Property(row => row.IsbnNormalized).IsRequired().HasMaxLength(13);
        builder.Property(row => row.IdentifierKind);
        builder.Property(row => row.Source);
        builder.Property(row => row.Rank);
        builder.Property(row => row.IsBest);
        builder.Property(row => row.DetectedUtc);
        builder.HasIndex(row => new { row.ExtractionArtifactId, row.IsbnNormalized, row.Source })
            .IsUnique()
            .HasDatabaseName("UX_ExtractedIsbnEvidence_Artifact_Value_Source");
        builder.HasIndex(row => new { row.BookId, row.IsbnNormalized })
            .HasDatabaseName("IX_ExtractedIsbnEvidence_Book_Value");
        builder.HasOne<BookRow>()
            .WithMany()
            .HasForeignKey(row => row.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ExtractionArtifactRow>()
            .WithMany()
            .HasForeignKey(row => row.ExtractionArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
