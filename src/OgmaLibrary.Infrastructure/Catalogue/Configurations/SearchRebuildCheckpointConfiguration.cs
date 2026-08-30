using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for resumable search rebuild checkpoints.</summary>
public sealed class SearchRebuildCheckpointConfiguration : IEntityTypeConfiguration<SearchRebuildCheckpointRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SearchRebuildCheckpointRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SearchRebuildCheckpoints", table =>
        {
            table.HasCheckConstraint("CK_SearchRebuildCheckpoints_Status", "Status BETWEEN 0 AND 3");
            table.HasCheckConstraint(
                "CK_SearchRebuildCheckpoints_Counts",
                "BooksAttempted >= 0 AND BooksIndexed >= 0 AND BooksFailed >= 0 AND ChunksWritten >= 0");
        });
        builder.HasKey(row => row.SearchRebuildCheckpointId);
        builder.Property(row => row.SearchRebuildCheckpointId).ValueGeneratedOnAdd();
        builder.Property(row => row.RebuildId).IsRequired().HasMaxLength(64);
        builder.Property(row => row.Status).HasDefaultValue(0);
        builder.Property(row => row.ErrorMessage).HasMaxLength(4096);
        builder.HasIndex(row => row.RebuildId).IsUnique();
        builder.HasIndex(row => new { row.Status, row.UpdatedUtc })
            .HasDatabaseName("IX_SearchRebuildCheckpoints_Status_UpdatedUtc");
    }
}
