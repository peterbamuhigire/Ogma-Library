using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for school AI entitlements.</summary>
public sealed class SchoolAiEntitlementConfiguration : IEntityTypeConfiguration<SchoolAiEntitlementRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolAiEntitlementRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SchoolAiEntitlements");
        builder.HasKey(row => row.ProfileId);
        builder.Property(row => row.ProfileId).HasMaxLength(36);
        builder.Property(row => row.DailyTokenBudget).HasDefaultValue(10_000);
        builder.Property(row => row.ClassDailyTokenBudget).HasDefaultValue(500_000);
        builder.Property(row => row.RateLimitQueriesPerMin).HasDefaultValue(5);
        builder.Property(row => row.UpdatedUtc).IsRequired();
    }
}
