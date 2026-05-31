using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Bookmarks table (HLD §3, FR-READ-007).
/// </summary>
public sealed class BookmarkConfiguration : IEntityTypeConfiguration<BookmarkRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookmarkRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Bookmarks");
        builder.HasKey(bm => bm.BookmarkId);
        builder.Property(bm => bm.BookmarkId).ValueGeneratedOnAdd();
        builder.Property(bm => bm.BookId).IsRequired().HasMaxLength(26);
        builder.Property(bm => bm.Page).IsRequired();
        builder.Property(bm => bm.Label).HasMaxLength(512);
        builder.Property(bm => bm.CreatedUtc);

        // Index for page-number lookup per book.
        builder.HasIndex(bm => new { bm.BookId, bm.Page })
            .HasDatabaseName("IX_Bookmarks_BookId_Page");
    }
}
