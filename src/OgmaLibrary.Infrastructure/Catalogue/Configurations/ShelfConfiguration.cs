using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Shelves table (HLD §3, FR-CAT-003).
/// </summary>
public sealed class ShelfConfiguration : IEntityTypeConfiguration<ShelfRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ShelfRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Shelves");
        builder.HasKey(s => s.ShelfId);
        builder.Property(s => s.ShelfId).IsRequired().HasMaxLength(26);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(256);
        builder.Property(s => s.ShelfType).HasDefaultValue(0);
        builder.Property(s => s.Query).HasMaxLength(4096);
        builder.Property(s => s.DisplayOrder).HasDefaultValue(0);
        builder.Property(s => s.CreatedUtc);

        // Name index for browsing.
        builder.HasIndex(s => s.Name).HasDatabaseName("IX_Shelves_Name");

        builder.HasMany(s => s.ShelfBooks)
            .WithOne(sb => sb.Shelf)
            .HasForeignKey(sb => sb.ShelfId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
