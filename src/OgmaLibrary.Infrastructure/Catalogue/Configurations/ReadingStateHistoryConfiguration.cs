using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF mapping and constraints for redacted reading-state history.</summary>
public sealed class ReadingStateHistoryConfiguration : IEntityTypeConfiguration<ReadingStateHistoryRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReadingStateHistoryRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReadingStateHistory", table =>
        {
            table.HasCheckConstraint("CK_ReadingStateHistory_Status", "ReadingStatus IS NULL OR ReadingStatus BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_ReadingStateHistory_Rating", "Rating IS NULL OR Rating BETWEEN 1 AND 5");
        });
        builder.HasKey(row => row.ReadingStateHistoryId);
        builder.Property(row => row.ReadingStateHistoryId).ValueGeneratedOnAdd();
        builder.Property(row => row.BookId).IsRequired().HasMaxLength(128);
        builder.Property(row => row.ReadingStatus);
        builder.Property(row => row.Rating);
        builder.Property(row => row.IsFavourite);
        builder.Property(row => row.Reason).IsRequired().HasMaxLength(256);
        builder.Property(row => row.ChangedUtc);
        builder.HasIndex(row => new { row.BookId, row.ChangedUtc })
            .HasDatabaseName("IX_ReadingStateHistory_Book_Changed");
        builder.HasOne(row => row.Book)
            .WithMany()
            .HasForeignKey(row => row.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
