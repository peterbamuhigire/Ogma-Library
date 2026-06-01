using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for LAN Host-mode settings.</summary>
public sealed class HostModeSettingsConfiguration : IEntityTypeConfiguration<HostModeSettingsRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HostModeSettingsRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("HostModeSettings");
        builder.HasKey(row => row.SettingsId);
        builder.Property(row => row.SettingsId).HasMaxLength(32);
        builder.Property(row => row.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(row => row.Port).IsRequired();
        builder.Property(row => row.ContentMode).IsRequired();
        builder.Property(row => row.UpdatedUtc).IsRequired();
        builder.HasData(new HostModeSettingsRow
        {
            SettingsId = "default",
            IsEnabled = false,
            Port = 7473,
            ContentMode = 0,
            DisplayName = "Ogma Library",
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        });
    }
}

