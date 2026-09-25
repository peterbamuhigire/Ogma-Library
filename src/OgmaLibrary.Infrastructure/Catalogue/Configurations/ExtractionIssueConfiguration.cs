using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for the ExtractionIssues table (Sept-23 Phase 06, T06.4).</summary>
public sealed class ExtractionIssueConfiguration : IEntityTypeConfiguration<ExtractionIssueRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExtractionIssueRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExtractionIssues", table =>
            table.HasCheckConstraint("CK_ExtractionIssues_Occurrences", "Occurrences >= 1"));
        builder.HasKey(issue => issue.ExtractionIssueId);
        builder.Property(issue => issue.ExtractionIssueId).ValueGeneratedOnAdd();
        builder.Property(issue => issue.BookId).IsRequired().HasMaxLength(26);
        builder.Property(issue => issue.PageIndex);
        builder.Property(issue => issue.ContentHash).IsRequired().HasMaxLength(64).HasDefaultValue(string.Empty);
        builder.Property(issue => issue.Code).IsRequired().HasMaxLength(128);
        builder.Property(issue => issue.Occurrences).HasDefaultValue(1);
        builder.Property(issue => issue.LastMessage).HasMaxLength(4096);
        builder.Property(issue => issue.FirstSeenUtc);
        builder.Property(issue => issue.LastSeenUtc);
        builder.HasIndex(issue => new { issue.BookId, issue.ContentHash, issue.PageIndex })
            .IsUnique()
            .HasDatabaseName("UX_ExtractionIssues_Book_Hash_Page");
    }
}
