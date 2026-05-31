using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the AnnotationsV2 table
/// (Phase 09 extended annotation with layer + normalized regions, FR-READ-008, ADR-0008).
/// </summary>
public sealed class AnnotationV2Configuration : IEntityTypeConfiguration<AnnotationV2Row>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AnnotationV2Row> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AnnotationsV2");
        builder.HasKey(a => a.AnnotationId);
        builder.Property(a => a.AnnotationId).IsRequired().HasMaxLength(26);
        builder.Property(a => a.BookId).IsRequired().HasMaxLength(26);
        builder.Property(a => a.LayerId).HasMaxLength(26);
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.RegionsJson).IsRequired().HasDefaultValue("[]");
        builder.Property(a => a.ColorKey).HasMaxLength(32);
        builder.Property(a => a.QuoteText).HasMaxLength(8192);
        builder.Property(a => a.NoteText).HasMaxLength(65536);
        builder.Property(a => a.CreatedUtc);
        builder.Property(a => a.ModifiedUtc);

        builder.HasIndex(a => new { a.BookId, a.LayerId })
            .HasDatabaseName("IX_AnnotationsV2_BookId_LayerId");

        builder.HasIndex(a => a.LayerId)
            .HasDatabaseName("IX_AnnotationsV2_LayerId");

        builder.HasOne(a => a.Book)
            .WithMany()
            .HasForeignKey(a => a.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Annotations belong to a layer; set null when layer deleted (not cascade).
        builder.HasOne(a => a.Layer)
            .WithMany(l => l.Annotations)
            .HasForeignKey(a => a.LayerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
