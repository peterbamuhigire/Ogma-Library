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
        builder.Property(v => v.ModelName).IsRequired().HasMaxLength(128);
        builder.Property(v => v.ModelVersion).IsRequired().HasMaxLength(128);
        builder.Property(v => v.DimensionCount).IsRequired();
        builder.Property(v => v.VectorBlob).IsRequired();
        builder.Property(v => v.GeneratedAtUtc);

        // Unique model-scoped lookup per chunk. Re-embedding with a new model
        // version creates a second durable row until erasure or cleanup.
        builder.HasIndex(v => new { v.ChunkId, v.ModelName, v.ModelVersion })
            .IsUnique()
            .HasDatabaseName("UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion");
    }
}
