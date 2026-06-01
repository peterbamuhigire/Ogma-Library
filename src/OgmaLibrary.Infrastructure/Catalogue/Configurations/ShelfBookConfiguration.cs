using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the ShelfBooks join table (HLD §3, FR-CAT-003).
/// </summary>
public sealed class ShelfBookConfiguration : IEntityTypeConfiguration<ShelfBookRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ShelfBookRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ShelfBooks");
        builder.HasKey(sb => new { sb.ShelfId, sb.BookId });
        builder.Property(sb => sb.ShelfId).IsRequired().HasMaxLength(26);
        builder.Property(sb => sb.BookId).IsRequired().HasMaxLength(26);
        builder.Property(sb => sb.AddedUtc);
        builder.Property(sb => sb.DisplayOrder).HasDefaultValue(0);
        builder.HasIndex(sb => new { sb.ShelfId, sb.BookId })
            .HasDatabaseName("IX_ShelfBooks_ShelfId_BookId");
    }
}
