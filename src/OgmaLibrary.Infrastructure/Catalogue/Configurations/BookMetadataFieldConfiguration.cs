using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the BookMetadataFields table
/// (HLD §3, FR-META-004).
/// </summary>
public sealed class BookMetadataFieldConfiguration : IEntityTypeConfiguration<BookMetadataFieldRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookMetadataFieldRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BookMetadataFields");
        builder.HasKey(m => m.FieldId);
        builder.Property(m => m.FieldId).ValueGeneratedOnAdd();
        builder.Property(m => m.BookId).IsRequired().HasMaxLength(26);
        builder.Property(m => m.FieldName).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Value).HasMaxLength(4096);
        builder.Property(m => m.Source).HasMaxLength(128);
        builder.Property(m => m.Confidence);
        builder.Property(m => m.IsOverridden).HasDefaultValue(false);

        // Provider-scoped field index.
        builder.HasIndex(m => new { m.BookId, m.FieldName, m.Source })
            .HasDatabaseName("IX_BookMetadataFields_BookId_FieldName_Source");
        // Smart-shelf field/value filter index.
        builder.HasIndex(m => new { m.FieldName, m.Value })
            .HasDatabaseName("IX_BookMetadataFields_FieldName_Value");
    }
}
