using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the ExtractedPages table (HLD §3, FR-SEARCH-002).
/// Populated by Phase 05 (ingestion pipeline).
/// </summary>
public sealed class ExtractedPageConfiguration : IEntityTypeConfiguration<ExtractedPageRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExtractedPageRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExtractedPages");
        builder.HasKey(p => p.ExtractedPageId);
        builder.Property(p => p.ExtractedPageId).ValueGeneratedOnAdd();
        builder.Property(p => p.BookId).IsRequired().HasMaxLength(26);
        builder.Property(p => p.PageNumber).IsRequired();
        builder.Property(p => p.TextContent);
        // Keep the database default for legacy SQL writers, but always send an
        // explicit CLR value so Full (enum zero) is not mistaken for "unset".
        builder.Property(p => p.ExtractionQuality).HasDefaultValue(2).ValueGeneratedNever();
        builder.Property(p => p.WordCount).HasDefaultValue(0);
        builder.Property(p => p.ContentHash).HasMaxLength(64).IsFixedLength();
        builder.Property(p => p.Source).IsRequired().HasMaxLength(32).HasDefaultValue("Extraction");
        builder.Property(p => p.ExtractionMethod).HasMaxLength(64);
        builder.Property(p => p.ExtractorVersion).IsRequired().HasMaxLength(128).HasDefaultValue("pdf-text-v1");
        builder.Property(p => p.IsSelectedText).HasDefaultValue(true);
        builder.Property(p => p.OcrConfidence).HasColumnType("REAL");
        builder.Property(p => p.OcrLanguage).HasMaxLength(32);
        builder.Property(p => p.OcrModelVersion).HasMaxLength(128);
        builder.Property(p => p.ExtractionUtc);

        // Index for page-number lookup per book.
        builder.HasIndex(p => new { p.BookId, p.PageNumber })
            .HasDatabaseName("IX_ExtractedPages_BookId_PageNumber");

        // Staleness lookup for extraction resume/cache checks.
        builder.HasIndex(p => new { p.BookId, p.ContentHash })
            .HasDatabaseName("IX_ExtractedPages_BookId_ContentHash");

        // Resume lookup for Phase 15 OCR jobs.
        builder.HasIndex(p => new { p.BookId, p.Source, p.PageNumber })
            .IsUnique()
            .HasDatabaseName("IX_ExtractedPages_BookId_Source_PageNumber");

        builder.HasMany(p => p.SearchChunks)
            .WithOne(c => c.ExtractedPage)
            .HasForeignKey(c => c.ExtractedPageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
