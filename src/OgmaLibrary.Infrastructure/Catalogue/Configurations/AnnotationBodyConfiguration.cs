using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the AnnotationBodies table (HLD §3, ADR-0008).
/// The body is a 1-to-1 dependent of its annotation.
/// </summary>
public sealed class AnnotationBodyConfiguration : IEntityTypeConfiguration<AnnotationBodyRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AnnotationBodyRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AnnotationBodies");
        builder.HasKey(b => b.AnnotationId);
        builder.Property(b => b.AnnotationId).IsRequired().HasMaxLength(26);
        builder.Property(b => b.QuoteText).HasMaxLength(8192);
        builder.Property(b => b.NoteText).HasMaxLength(65536);
        builder.Property(b => b.RectJson).HasMaxLength(1024);
    }
}
