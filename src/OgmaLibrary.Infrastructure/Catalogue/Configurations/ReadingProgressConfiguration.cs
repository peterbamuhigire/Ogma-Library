using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the ReadingProgress table (HLD §3, FR-READ-001).
/// PK is BookId — one progress record per book.
/// </summary>
public sealed class ReadingProgressConfiguration : IEntityTypeConfiguration<ReadingProgressRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReadingProgressRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReadingProgress");
        builder.HasKey(r => r.BookId);
        builder.Property(r => r.BookId).IsRequired().HasMaxLength(26);
        builder.Property(r => r.CurrentPage).HasDefaultValue(0);
        builder.Property(r => r.ScrollOffsetPx).HasDefaultValue(0.0);
        builder.Property(r => r.LastReadUtc);
        builder.Property(r => r.TotalPagesRead).HasDefaultValue(0);
        builder.Property(r => r.CompletionPct).HasDefaultValue(0.0);
        builder.Property(r => r.Status).HasDefaultValue(0);
    }
}
