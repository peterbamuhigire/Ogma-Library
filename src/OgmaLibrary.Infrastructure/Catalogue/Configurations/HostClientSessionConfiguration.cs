using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for LAN Host client sessions.</summary>
public sealed class HostClientSessionConfiguration : IEntityTypeConfiguration<HostClientSessionRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HostClientSessionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("HostClientSessions");
        builder.HasKey(row => row.TokenHash);
        builder.Property(row => row.TokenHash).HasMaxLength(64);
        builder.Property(row => row.ClientId).HasMaxLength(128).IsRequired();
        builder.Property(row => row.Role).HasMaxLength(64).IsRequired();
        builder.Property(row => row.IssuedUtc).IsRequired();
        builder.Property(row => row.ExpiresUtc).IsRequired();
        builder.HasIndex(row => new { row.ClientId, row.ExpiresUtc })
            .HasDatabaseName("IX_HostClientSessions_ClientId_ExpiresUtc");
        builder.HasIndex(row => row.RevokedUtc)
            .HasDatabaseName("IX_HostClientSessions_RevokedUtc");
    }
}
