using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for classroom library publishing policies.</summary>
public sealed class LibraryPublishSettingsConfiguration : IEntityTypeConfiguration<LibraryPublishSettingsRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LibraryPublishSettingsRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LibraryPublishSettings");
        builder.HasKey(row => row.LibraryRootId);
        builder.Property(row => row.LibraryRootId).HasMaxLength(128);
        builder.Property(row => row.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(row => row.SourcePath).HasMaxLength(4096).IsRequired();
        builder.Property(row => row.IsPublished).HasDefaultValue(false);
        builder.Property(row => row.AiTier).HasDefaultValue(1);
        builder.Property(row => row.UpdatedUtc).IsRequired();
        builder.HasIndex(row => row.IsPublished)
            .HasDatabaseName("IX_LibraryPublishSettings_IsPublished");
        builder.HasIndex(row => row.SourcePath)
            .IsUnique()
            .HasDatabaseName("UX_LibraryPublishSettings_SourcePath");
    }
}
