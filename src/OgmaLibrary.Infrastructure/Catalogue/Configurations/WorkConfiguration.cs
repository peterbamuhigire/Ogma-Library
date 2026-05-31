using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Works table (HLD §3 — Work/Edition layer).
/// Schema-only in Phase 04; merge/split UI is deferred to Phase 06/07 (WP9).
/// </summary>
public sealed class WorkConfiguration : IEntityTypeConfiguration<WorkRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WorkRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Works");
        builder.HasKey(w => w.WorkId);
        builder.Property(w => w.WorkId).ValueGeneratedOnAdd();
        builder.Property(w => w.CanonicalTitle).HasMaxLength(1024);
        builder.Property(w => w.CanonicalAuthorId);

        builder.HasMany(w => w.Editions)
            .WithOne(e => e.Work)
            .HasForeignKey(e => e.WorkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
