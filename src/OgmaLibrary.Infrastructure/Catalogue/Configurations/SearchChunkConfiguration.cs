using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the SearchChunks table (HLD §3, FR-SEARCH-002).
/// Populated by Phase 10 (search index).
/// </summary>
public sealed class SearchChunkConfiguration : IEntityTypeConfiguration<SearchChunkRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SearchChunkRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SearchChunks");
        builder.HasKey(c => c.ChunkId);
        builder.Property(c => c.ChunkId).ValueGeneratedOnAdd();
        builder.Property(c => c.BookId).IsRequired().HasMaxLength(26);
        builder.Property(c => c.ExtractedPageId);
        builder.Property(c => c.ChunkIndex).IsRequired();
        builder.Property(c => c.ChunkText);
        builder.Property(c => c.TokenCount).HasDefaultValue(0);

        // Index for chunk ordering per book.
        builder.HasIndex(c => new { c.BookId, c.ChunkIndex })
            .HasDatabaseName("IX_SearchChunks_BookId_ChunkIndex");

        builder.HasMany(c => c.EmbeddingVectors)
            .WithOne(v => v.Chunk)
            .HasForeignKey(v => v.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
