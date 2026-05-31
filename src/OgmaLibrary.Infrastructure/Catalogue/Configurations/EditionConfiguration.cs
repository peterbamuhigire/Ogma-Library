using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Editions table (HLD §3 — Work/Edition layer).
/// Schema-only in Phase 04; Books carry a nullable FK to this table (WP9).
/// </summary>
public sealed class EditionConfiguration : IEntityTypeConfiguration<EditionRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EditionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Editions");
        builder.HasKey(e => e.EditionId);
        builder.Property(e => e.EditionId).ValueGeneratedOnAdd();
        builder.Property(e => e.WorkId).IsRequired();
        builder.Property(e => e.Language).HasMaxLength(8);
        builder.Property(e => e.PublicationYear);
        builder.Property(e => e.Publisher).HasMaxLength(512);
    }
}
