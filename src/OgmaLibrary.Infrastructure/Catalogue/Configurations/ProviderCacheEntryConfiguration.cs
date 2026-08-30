using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for bounded metadata provider cache entries.</summary>
public sealed class ProviderCacheEntryConfiguration : IEntityTypeConfiguration<ProviderCacheEntryRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProviderCacheEntryRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProviderCacheEntries", table =>
        {
            table.HasCheckConstraint("CK_ProviderCacheEntries_Contract", "ContractVersion > 0");
            table.HasCheckConstraint("CK_ProviderCacheEntries_Response", "length(ResponseJson) <= 262144");
        });
        builder.HasKey(row => row.ProviderCacheEntryId);
        builder.Property(row => row.ProviderCacheEntryId).ValueGeneratedOnAdd();
        builder.Property(row => row.Provider).IsRequired().HasMaxLength(128);
        builder.Property(row => row.QueryKey).IsRequired().HasMaxLength(2048);
        builder.Property(row => row.ResponseJson).IsRequired().HasMaxLength(262144);
        builder.Property(row => row.ContractVersion).HasDefaultValue(1);
        builder.HasIndex(row => new { row.Provider, row.QueryKey })
            .IsUnique()
            .HasDatabaseName("UX_ProviderCacheEntries_Provider_Query");
    }
}
