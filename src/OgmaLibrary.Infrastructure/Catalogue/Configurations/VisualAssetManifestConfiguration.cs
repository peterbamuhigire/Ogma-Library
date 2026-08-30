using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF mapping and integrity constraints for visual asset manifests.</summary>
public sealed class VisualAssetManifestConfiguration : IEntityTypeConfiguration<VisualAssetManifestRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VisualAssetManifestRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("VisualAssetManifests", table =>
        {
            table.HasCheckConstraint("CK_VisualAssets_Kind", "Kind BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_VisualAssets_Status", "Status BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_VisualAssets_Dimensions", "WidthPx > 0 AND HeightPx > 0");
            table.HasCheckConstraint("CK_VisualAssets_GenerationVersion", "GenerationVersion > 0");
        });

        builder.HasKey(asset => new { asset.BookId, asset.Kind, asset.Variant });
        builder.Property(asset => asset.BookId).IsRequired().HasMaxLength(128);
        builder.Property(asset => asset.Kind);
        builder.Property(asset => asset.Variant).IsRequired().HasMaxLength(64);
        builder.Property(asset => asset.RelativePath).IsRequired().HasMaxLength(4096);
        builder.Property(asset => asset.Source).IsRequired().HasMaxLength(64);
        builder.Property(asset => asset.SourceContentHash).HasMaxLength(64);
        builder.Property(asset => asset.WidthPx);
        builder.Property(asset => asset.HeightPx);
        builder.Property(asset => asset.Format).IsRequired().HasMaxLength(16);
        builder.Property(asset => asset.GenerationVersion);
        builder.Property(asset => asset.Status);
        builder.Property(asset => asset.IsCustom);
        builder.Property(asset => asset.CreatedUtc);
        builder.Property(asset => asset.UpdatedUtc);
        builder.HasIndex(asset => new { asset.BookId, asset.Kind, asset.Status })
            .HasDatabaseName("IX_VisualAssets_Book_Kind_Status");
        builder.HasOne(asset => asset.Book)
            .WithMany(book => book.VisualAssets)
            .HasForeignKey(asset => asset.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
