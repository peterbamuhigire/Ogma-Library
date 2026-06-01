using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for immutable AI audit events.</summary>
public sealed class AiAuditEventConfiguration : IEntityTypeConfiguration<AiAuditEventRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiAuditEventRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiAuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Tier).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Model).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PromptTokens);
        builder.Property(e => e.CompletionTokens);
        builder.Property(e => e.PromptCacheTokens);
        builder.Property(e => e.EstimatedCostUsd).HasColumnType("REAL");
        builder.Property(e => e.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ResponseHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.QueryHistoryEntryId).HasMaxLength(64);
        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("IX_AiAuditEvents_OccurredAt");
        builder.HasIndex(e => e.QueryHistoryEntryId)
            .HasDatabaseName("IX_AiAuditEvents_QueryHistoryEntryId");
    }
}
