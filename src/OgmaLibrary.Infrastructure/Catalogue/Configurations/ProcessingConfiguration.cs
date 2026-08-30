using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for durable scan sessions and leased stages.</summary>
public sealed class ScanSessionConfiguration : IEntityTypeConfiguration<ScanSessionRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ScanSessionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ScanSessions", table =>
        {
            table.HasCheckConstraint("CK_ScanSessions_Status", "Status BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_ScanSessions_RootId", "length(LibraryRootId) = 26");
        });
        builder.HasKey(row => row.ScanSessionId);
        builder.Property(row => row.ScanSessionId).ValueGeneratedOnAdd();
        builder.Property(row => row.LibraryRootId).IsRequired().HasMaxLength(26);
        builder.Property(row => row.Status).HasDefaultValue(0);
        builder.Property(row => row.StartedUtc);
        builder.Property(row => row.CompletedUtc);
        builder.HasIndex(row => new { row.LibraryRootId, row.Status })
            .HasDatabaseName("IX_ScanSessions_Root_Status");
        builder.HasOne<LibraryRootRow>()
            .WithMany()
            .HasForeignKey(row => row.LibraryRootId)
            .OnDelete(DeleteBehavior.Restrict);
    }

}

/// <summary>EF constraints for leased stage executions.</summary>
public sealed class StageExecutionConfiguration : IEntityTypeConfiguration<StageExecutionRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StageExecutionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("StageExecutions", table =>
        {
            table.HasCheckConstraint("CK_StageExecutions_Status", "Status BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_StageExecutions_Attempt", "Attempt >= 0");
        });
        builder.HasKey(row => row.StageExecutionId);
        builder.Property(row => row.StageExecutionId).ValueGeneratedOnAdd();
        builder.Property(row => row.StageName).IsRequired().HasMaxLength(128);
        builder.Property(row => row.SubjectKey).IsRequired().HasMaxLength(512);
        builder.Property(row => row.LeaseOwner).HasMaxLength(128);
        builder.Property(row => row.ErrorCode).HasMaxLength(128);
        builder.Property(row => row.ErrorMessage).HasMaxLength(4096);
        builder.HasIndex(row => new { row.ScanSessionId, row.StageName, row.SubjectKey })
            .IsUnique()
            .HasDatabaseName("UX_StageExecutions_Idempotency");
        builder.HasIndex(row => new { row.StageName, row.Status, row.NextAttemptUtc })
            .HasDatabaseName("IX_StageExecutions_Claim");
        builder.HasOne<ScanSessionRow>()
            .WithMany()
            .HasForeignKey(row => row.ScanSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
