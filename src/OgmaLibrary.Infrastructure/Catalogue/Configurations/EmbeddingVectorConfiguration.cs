using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the EmbeddingVectors table
/// (HLD §3, FR-SEARCH-004). Binary vector blobs are stored as BLOB columns.
/// Populated by Phase 11 (embeddings).
/// </summary>
public sealed class EmbeddingVectorConfiguration : IEntityTypeConfiguration<EmbeddingVectorRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmbeddingVectorRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmbeddingVectors");
        builder.HasKey(v => v.VectorId);
        builder.Property(v => v.VectorId).ValueGeneratedOnAdd();
        builder.Property(v => v.ChunkId).IsRequired();
        builder.Property(v => v.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(v => v.DimensionCount).IsRequired();
        builder.Property(v => v.VectorBlob);
        builder.Property(v => v.CreatedUtc);

        // Index for model-scoped lookup per chunk.
        builder.HasIndex(v => new { v.ChunkId, v.ModelId })
            .HasDatabaseName("IX_EmbeddingVectors_ChunkId_ModelId");
    }
}
