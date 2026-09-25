using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for the FileIssues table (Sept-23 Phase 05, K21).</summary>
public sealed class FileIssueConfiguration : IEntityTypeConfiguration<FileIssueRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileIssueRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("FileIssues", table =>
            table.HasCheckConstraint("CK_FileIssues_Reason", "Reason BETWEEN 1 AND 3"));
        builder.HasKey(issue => issue.FileIssueId);
        builder.Property(issue => issue.FileIssueId).ValueGeneratedOnAdd();
        builder.Property(issue => issue.LibraryRootId).HasMaxLength(26);
        builder.Property(issue => issue.RelativePath).IsRequired().HasMaxLength(4096);
        builder.Property(issue => issue.Reason);
        builder.Property(issue => issue.SizeBytes);
        builder.Property(issue => issue.MtimeTicks);
        builder.Property(issue => issue.IsIgnored).HasDefaultValue(false);
        builder.Property(issue => issue.DetectedUtc);
        builder.Property(issue => issue.LastCheckedUtc);
        builder.HasIndex(issue => new { issue.LibraryRootId, issue.RelativePath })
            .IsUnique()
            .HasDatabaseName("UX_FileIssues_LibraryRootId_RelativePath");
    }
}
