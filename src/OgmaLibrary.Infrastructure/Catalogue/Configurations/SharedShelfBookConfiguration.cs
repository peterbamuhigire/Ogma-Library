using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for shared shelf book assignments.</summary>
public sealed class SharedShelfBookConfiguration : IEntityTypeConfiguration<SharedShelfBookRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SharedShelfBookRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SharedShelfBooks");
        builder.HasKey(row => new { row.ShelfId, row.BookId });
        builder.Property(row => row.ShelfId).HasMaxLength(64);
        builder.Property(row => row.BookId).HasMaxLength(26);
        builder.Property(row => row.AddedUtc).IsRequired();
        builder.HasOne(row => row.Shelf)
            .WithMany(shelf => shelf.Books)
            .HasForeignKey(row => row.ShelfId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(row => row.Book)
            .WithMany()
            .HasForeignKey(row => row.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(row => row.BookId)
            .HasDatabaseName("IX_SharedShelfBooks_BookId");
    }
}
