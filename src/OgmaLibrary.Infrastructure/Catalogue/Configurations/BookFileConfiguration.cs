using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the BookFiles table (HLD §3, FR-LIB-004).
/// </summary>
public sealed class BookFileConfiguration : IEntityTypeConfiguration<BookFileRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookFileRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BookFiles");
        builder.HasKey(f => f.BookFileId);
        builder.Property(f => f.BookFileId).ValueGeneratedOnAdd();
        builder.Property(f => f.BookId).IsRequired().HasMaxLength(26);
        builder.Property(f => f.RelativePath).IsRequired().HasMaxLength(4096);
        builder.Property(f => f.FileStatus).HasDefaultValue(0);
        builder.Property(f => f.LastSeenUtc);

        // Index for quick presence queries per book.
        builder.HasIndex(f => new { f.BookId, f.FileStatus })
            .HasDatabaseName("IX_BookFiles_BookId_FileStatus");
        // Index for path-based lookup.
        builder.HasIndex(f => f.RelativePath)
            .HasDatabaseName("IX_BookFiles_RelativePath");
    }
}
