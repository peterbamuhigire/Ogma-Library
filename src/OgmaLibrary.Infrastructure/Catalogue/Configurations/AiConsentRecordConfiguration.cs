using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF Core configuration for AI consent records.</summary>
public sealed class AiConsentRecordConfiguration : IEntityTypeConfiguration<AiConsentRecordRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiConsentRecordRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiConsentRecords");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(64);
        builder.Property(c => c.Tier).IsRequired();
        builder.Property(c => c.Provider).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Scope).HasMaxLength(128).IsRequired();
        builder.Property(c => c.GrantedAt).IsRequired();
        builder.Property(c => c.RevokedAt);
        builder.HasIndex(c => new { c.Tier, c.Provider, c.Scope })
            .HasDatabaseName("IX_AiConsentRecords_Tier_Provider_Scope");
    }
}
