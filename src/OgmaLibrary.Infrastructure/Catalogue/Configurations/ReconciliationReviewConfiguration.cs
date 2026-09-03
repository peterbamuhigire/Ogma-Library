using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for ambiguous filesystem relocation reviews.</summary>
public sealed class ReconciliationReviewConfiguration : IEntityTypeConfiguration<ReconciliationReviewRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReconciliationReviewRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReconciliationReviews", table =>
        {
            table.HasCheckConstraint("CK_ReconciliationReviews_Status", "Status BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_ReconciliationReviews_RootId", "length(LibraryRootId) = 26");
            table.HasCheckConstraint("CK_ReconciliationReviews_OccurrenceId", "length(FileOccurrenceId) = 26");
        });
        builder.HasKey(row => row.ReconciliationReviewId);
        builder.Property(row => row.ReconciliationReviewId).ValueGeneratedOnAdd();
        builder.Property(row => row.LibraryRootId).IsRequired().HasMaxLength(26);
        builder.Property(row => row.FileOccurrenceId).IsRequired().HasMaxLength(26);
        builder.Property(row => row.ReasonCode).IsRequired().HasMaxLength(128);
        builder.Property(row => row.CandidatePathsJson).IsRequired().HasMaxLength(65536);
        builder.Property(row => row.Status).HasDefaultValue(0);
        builder.HasIndex(row => new { row.LibraryRootId, row.FileOccurrenceId, row.Status })
            .HasDatabaseName("IX_ReconciliationReviews_Occurrence_Status");
        builder.HasOne<LibraryRootRow>()
            .WithMany()
            .HasForeignKey(row => row.LibraryRootId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
