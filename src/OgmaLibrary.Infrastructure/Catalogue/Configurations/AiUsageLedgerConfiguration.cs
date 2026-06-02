using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for classroom AI usage ledger rows.</summary>
public sealed class AiUsageLedgerConfiguration : IEntityTypeConfiguration<AiUsageLedgerRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiUsageLedgerRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiUsageLedger");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasMaxLength(64);
        builder.Property(row => row.ProfileId).HasMaxLength(36).IsRequired();
        builder.Property(row => row.Date).HasMaxLength(10).IsRequired();
        builder.Property(row => row.TokensUsed).HasDefaultValue(0);
        builder.Property(row => row.QueryCount).HasDefaultValue(0);
        builder.Property(row => row.EstimatedCostUsd).HasColumnType("REAL").HasDefaultValue(0m);
        builder.Property(row => row.UpdatedUtc).IsRequired();
        builder.HasIndex(row => new { row.ProfileId, row.Date })
            .IsUnique()
            .HasDatabaseName("UX_AiUsageLedger_ProfileId_Date");
        builder.HasIndex(row => row.Date)
            .HasDatabaseName("IX_AiUsageLedger_Date");
    }
}
