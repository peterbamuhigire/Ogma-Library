using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the AnnotationLayers table
/// (Phase 09 world-class addition).
/// </summary>
public sealed class AnnotationLayerConfiguration : IEntityTypeConfiguration<AnnotationLayerRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AnnotationLayerRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AnnotationLayers");
        builder.HasKey(l => l.LayerId);
        builder.Property(l => l.LayerId).IsRequired().HasMaxLength(26);
        builder.Property(l => l.BookId).IsRequired().HasMaxLength(26);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(256);
        builder.Property(l => l.Color).IsRequired().HasMaxLength(32).HasDefaultValue("#FFD700");
        builder.Property(l => l.IsVisible).IsRequired().HasDefaultValue(true);
        builder.Property(l => l.SortOrder).IsRequired().HasDefaultValue(0);

        builder.HasIndex(l => l.BookId)
            .HasDatabaseName("IX_AnnotationLayers_BookId");

        builder.HasOne(l => l.Book)
            .WithMany()
            .HasForeignKey(l => l.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
