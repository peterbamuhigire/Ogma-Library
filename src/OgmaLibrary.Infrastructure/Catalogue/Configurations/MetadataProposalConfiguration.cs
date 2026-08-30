using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for metadata review proposals.</summary>
public sealed class MetadataProposalConfiguration : IEntityTypeConfiguration<MetadataProposalRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MetadataProposalRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("MetadataProposals", table =>
        {
            table.HasCheckConstraint("CK_MetadataProposals_Status", "Status BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_MetadataProposals_Confidence", "Confidence BETWEEN 0.0 AND 1.0");
            table.HasCheckConstraint("CK_MetadataProposals_Alternatives", "length(AlternativesJson) <= 65536");
        });
        builder.HasKey(row => row.MetadataProposalId);
        builder.Property(row => row.MetadataProposalId).ValueGeneratedOnAdd();
        builder.Property(row => row.BookId).IsRequired().HasMaxLength(128);
        builder.Property(row => row.FieldName).IsRequired().HasMaxLength(128);
        builder.Property(row => row.ProposedValue).HasMaxLength(4096);
        builder.Property(row => row.CurrentValue).HasMaxLength(4096);
        builder.Property(row => row.Source).IsRequired().HasMaxLength(128);
        builder.Property(row => row.AlternativesJson).IsRequired().HasMaxLength(65536);
        builder.Property(row => row.Status).HasDefaultValue(0);
        builder.HasIndex(row => new { row.BookId, row.Status, row.CreatedUtc })
            .HasDatabaseName("IX_MetadataProposals_Book_Status_Created");
        builder.HasOne<BookRow>()
            .WithMany()
            .HasForeignKey(row => row.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
