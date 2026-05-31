using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Jobs table (HLD §3, NFR-OGMA-009).
/// The unique index on IdempotencyKey is the core idempotency guarantee.
/// </summary>
public sealed class JobConfiguration : IEntityTypeConfiguration<JobRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JobRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Jobs");
        builder.HasKey(j => j.JobId);
        builder.Property(j => j.JobId).ValueGeneratedOnAdd();
        builder.Property(j => j.JobType).IsRequired().HasMaxLength(128);
        builder.Property(j => j.IdempotencyKey).IsRequired().HasMaxLength(256);
        builder.Property(j => j.Status).HasDefaultValue(0);
        builder.Property(j => j.BookId).HasMaxLength(26);
        builder.Property(j => j.Payload).HasMaxLength(65536);
        builder.Property(j => j.StartedUtc);
        builder.Property(j => j.CompletedUtc);
        builder.Property(j => j.ErrorMessage).HasMaxLength(4096);
        builder.Property(j => j.RetryCount).HasDefaultValue(0);

        // The idempotency constraint — prevents duplicate job submissions (NFR-OGMA-009).
        builder.HasIndex(j => j.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UQ_Jobs_IdempotencyKey");

        // Status + type index for queue polling.
        builder.HasIndex(j => new { j.Status, j.JobType })
            .HasDatabaseName("IX_Jobs_Status_JobType");
    }
}
