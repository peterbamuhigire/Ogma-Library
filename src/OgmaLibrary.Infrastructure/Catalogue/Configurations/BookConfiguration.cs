using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Books table (HLD §3).
/// Defines the primary key, indexes, foreign keys, and column types without using
/// data annotations on the entity — keeping domain entities annotation-free (ADR-0005).
/// </summary>
public sealed class BookConfiguration : IEntityTypeConfiguration<BookRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Books");
        builder.HasKey(b => b.BookId);
        builder.Property(b => b.BookId).IsRequired().HasMaxLength(26);
        builder.Property(b => b.Title).HasMaxLength(1024);
        builder.Property(b => b.RelativePath).HasMaxLength(4096);
        builder.Property(b => b.Sha256Hash).HasMaxLength(64).IsFixedLength();
        builder.Property(b => b.IsbnNormalized).HasMaxLength(13);
        builder.Property(b => b.Doi).HasMaxLength(512);
        builder.Property(b => b.PdfFingerprint).HasMaxLength(512);
        builder.Property(b => b.Status).HasDefaultValue(0);
        builder.Property(b => b.Rating);
        builder.Property(b => b.Year);
        builder.Property(b => b.SizeBytes);
        builder.Property(b => b.MtimeTicks);
        builder.Property(b => b.EditionId);

        // Availability index: query books by relative path quickly.
        builder.HasIndex(b => b.RelativePath).HasDatabaseName("IX_Books_RelativePath");
        // Content identity index: fast lookup by hash after rename.
        builder.HasIndex(b => b.Sha256Hash).HasDatabaseName("IX_Books_Sha256Hash");
        // ISBN lookup index: fast ISBN-based identity tier.
        builder.HasIndex(b => b.IsbnNormalized).HasDatabaseName("IX_Books_IsbnNormalized");
        // Status filter index.
        builder.HasIndex(b => b.Status).HasDatabaseName("IX_Books_Status");

        // Relationships
        builder.HasMany(b => b.BookFiles)
            .WithOne(f => f.Book)
            .HasForeignKey(f => f.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.BookAuthors)
            .WithOne(ba => ba.Book)
            .HasForeignKey(ba => ba.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.ShelfBooks)
            .WithOne(sb => sb.Book)
            .HasForeignKey(sb => sb.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.MetadataFields)
            .WithOne(m => m.Book)
            .HasForeignKey(m => m.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.ReadingProgress)
            .WithOne(r => r.Book)
            .HasForeignKey<ReadingProgressRow>(r => r.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Bookmarks)
            .WithOne(bm => bm.Book)
            .HasForeignKey(bm => bm.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Annotations)
            .WithOne(a => a.Book)
            .HasForeignKey(a => a.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Edition)
            .WithMany(e => e.Books)
            .HasForeignKey(b => b.EditionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
