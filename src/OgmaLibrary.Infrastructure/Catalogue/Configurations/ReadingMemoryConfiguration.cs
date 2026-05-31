using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the ReadingMemory table
/// (Phase 09 world-class addition).
/// </summary>
public sealed class ReadingMemoryConfiguration : IEntityTypeConfiguration<ReadingMemoryRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReadingMemoryRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReadingMemory");
        builder.HasKey(m => m.BookId);
        builder.Property(m => m.BookId).IsRequired().HasMaxLength(26);
        builder.Property(m => m.OpenedBecause).HasMaxLength(8192);
        builder.Property(m => m.KeyInsight).HasMaxLength(8192);
        builder.Property(m => m.OpenQuestions).HasMaxLength(8192);
        builder.Property(m => m.Disposition);
        builder.Property(m => m.CreatedAtUtc);
        builder.Property(m => m.UpdatedAtUtc);

        builder.HasOne(m => m.Book)
            .WithOne(b => b.ReadingMemory)
            .HasForeignKey<ReadingMemoryRow>(m => m.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
