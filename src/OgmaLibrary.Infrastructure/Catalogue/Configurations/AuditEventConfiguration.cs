using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the AuditEvents table
/// (HLD §3, NFR-PROD-013, CTRL-OGMA-018). Append-only; no update or delete operations.
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEventRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).ValueGeneratedOnAdd();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.EntityId).HasMaxLength(256);
        builder.Property(e => e.EntityType).HasMaxLength(128);
        builder.Property(e => e.ActorId).HasMaxLength(256);
        builder.Property(e => e.BeforeJson);
        builder.Property(e => e.AfterJson);
        builder.Property(e => e.Timestamp);
        builder.Property(e => e.IsLocalOnly).HasDefaultValue(true);

        // Descending timestamp for recent-audit queries.
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_AuditEvents_Timestamp");
        // Entity-scoped lookup for provenance.
        builder.HasIndex(e => new { e.EntityId, e.EntityType })
            .HasDatabaseName("IX_AuditEvents_EntityId_EntityType");
    }
}
