using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for administrator-curated shared shelves.</summary>
public sealed class SharedShelfConfiguration : IEntityTypeConfiguration<SharedShelfRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SharedShelfRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SharedShelves");
        builder.HasKey(row => row.ShelfId);
        builder.Property(row => row.ShelfId).HasMaxLength(64);
        builder.Property(row => row.Name).HasMaxLength(256).IsRequired();
        builder.Property(row => row.Description).HasMaxLength(2048);
        builder.Property(row => row.Visibility).HasDefaultValue(0);
        builder.Property(row => row.GroupIdsJson).HasColumnType("TEXT").HasDefaultValue("[]");
        builder.Property(row => row.CreatedUtc).IsRequired();
        builder.Property(row => row.UpdatedUtc).IsRequired();
        builder.Property(row => row.IsDeleted).HasDefaultValue(false);
        builder.HasIndex(row => new { row.Visibility, row.IsDeleted })
            .HasDatabaseName("IX_SharedShelves_Visibility_IsDeleted");
        builder.HasIndex(row => row.Name)
            .HasDatabaseName("IX_SharedShelves_Name");
    }
}
