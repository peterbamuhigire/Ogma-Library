using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for the durable semantic-index pointer.</summary>
public sealed class EmbeddingIndexStateConfiguration : IEntityTypeConfiguration<EmbeddingIndexStateRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmbeddingIndexStateRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("EmbeddingIndexState", table =>
        {
            table.HasCheckConstraint(
                "CK_EmbeddingIndexState_ActiveVersion",
                "length(ActiveIndexVersion) BETWEEN 1 AND 128");
            table.HasCheckConstraint(
                "CK_EmbeddingIndexState_StagingVersion",
                "StagingIndexVersion IS NULL OR length(StagingIndexVersion) BETWEEN 1 AND 128");
        });
        builder.HasKey(row => row.StateKey);
        builder.Property(row => row.StateKey).HasMaxLength(32);
        builder.Property(row => row.ActiveIndexVersion).IsRequired().HasMaxLength(128);
        builder.Property(row => row.StagingIndexVersion).HasMaxLength(128);
        builder.Property(row => row.UpdatedUtc).IsRequired();
    }
}
