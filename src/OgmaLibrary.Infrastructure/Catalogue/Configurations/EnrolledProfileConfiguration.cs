using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for school-managed classroom profiles.</summary>
public sealed class EnrolledProfileConfiguration : IEntityTypeConfiguration<EnrolledProfileRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EnrolledProfileRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EnrolledProfiles");
        builder.HasKey(row => row.ProfileId);
        builder.Property(row => row.ProfileId).HasMaxLength(36);
        builder.Property(row => row.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(row => row.Role).HasMaxLength(64).IsRequired();
        builder.Property(row => row.EnrollmentToken).HasMaxLength(128);
        builder.Property(row => row.EnrolledUtc).IsRequired();
        builder.HasIndex(row => row.EnrollmentToken)
            .IsUnique()
            .HasDatabaseName("UX_EnrolledProfiles_EnrollmentToken");
        builder.HasIndex(row => new { row.Role, row.RevokedUtc })
            .HasDatabaseName("IX_EnrolledProfiles_Role_RevokedUtc");
    }
}
