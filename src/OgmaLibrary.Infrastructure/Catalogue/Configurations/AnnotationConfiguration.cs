using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Annotations table (HLD §3, FR-READ-008, ADR-0008).
/// </summary>
public sealed class AnnotationConfiguration : IEntityTypeConfiguration<AnnotationRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AnnotationRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Annotations");
        builder.HasKey(a => a.AnnotationId);
        builder.Property(a => a.AnnotationId).IsRequired().HasMaxLength(26);
        builder.Property(a => a.BookId).IsRequired().HasMaxLength(26);
        builder.Property(a => a.Page).IsRequired();
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.CreatedUtc);
        builder.Property(a => a.ModifiedUtc);
        builder.Property(a => a.ColorKey).HasMaxLength(32);

        // Index for page-based queries per book.
        builder.HasIndex(a => new { a.BookId, a.Page })
            .HasDatabaseName("IX_Annotations_BookId_Page");

        builder.HasOne(a => a.Body)
            .WithOne(b => b.Annotation)
            .HasForeignKey<AnnotationBodyRow>(b => b.AnnotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
