using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the AiQueryHistory table
/// (HLD §3, FR-AI-009, NFR-PROD-014). Supports soft-delete via IsDeleted.
/// Populated by Phase 12 (AI gateway).
/// </summary>
public sealed class AiQueryHistoryConfiguration : IEntityTypeConfiguration<AiQueryHistoryRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiQueryHistoryRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiQueryHistory");
        builder.HasKey(q => q.QueryId);
        builder.Property(q => q.QueryId).ValueGeneratedOnAdd();
        builder.Property(q => q.QueryText).HasMaxLength(8192);
        builder.Property(q => q.ProviderKey).HasMaxLength(64);
        builder.Property(q => q.ModelId).HasMaxLength(128);
        builder.Property(q => q.PrivacyTier).HasDefaultValue(0);
        builder.Property(q => q.RequestPayloadHash).HasMaxLength(64);
        builder.Property(q => q.ResponseSummary).HasMaxLength(4096);
        builder.Property(q => q.TokensIn);
        builder.Property(q => q.TokensOut);
        builder.Property(q => q.CostEstimate).HasColumnType("REAL");
        builder.Property(q => q.CreatedUtc);
        builder.Property(q => q.IsDeleted).HasDefaultValue(false);

        // Descending timestamp index for recent-history queries.
        builder.HasIndex(q => q.CreatedUtc)
            .HasDatabaseName("IX_AiQueryHistory_CreatedUtc");
    }
}
