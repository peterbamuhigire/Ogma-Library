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
        builder.Property(p => p.ExtractionQuality).HasDefaultValue(2);
        builder.Property(p => p.WordCount).HasDefaultValue(0);
        builder.Property(p => p.ContentHash).HasMaxLength(64).IsFixedLength();
        builder.Property(p => p.ExtractionMethod).HasMaxLength(64);
        builder.Property(p => p.ExtractionUtc);

        // Index for page-number lookup per book.
        builder.HasIndex(p => new { p.BookId, p.PageNumber })
            .IsUnique()
            .HasDatabaseName("IX_ExtractedPages_BookId_PageNumber");

        // Staleness lookup for extraction resume/cache checks.
        builder.HasIndex(p => new { p.BookId, p.ContentHash })
            .HasDatabaseName("IX_ExtractedPages_BookId_ContentHash");

        builder.HasMany(p => p.SearchChunks)
            .WithOne(c => c.ExtractedPage)
            .HasForeignKey(c => c.ExtractedPageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
