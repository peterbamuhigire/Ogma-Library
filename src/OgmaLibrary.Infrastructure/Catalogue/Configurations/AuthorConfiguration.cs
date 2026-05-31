using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the Authors table (HLD §3).
/// </summary>
public sealed class AuthorConfiguration : IEntityTypeConfiguration<AuthorRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthorRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Authors");
        builder.HasKey(a => a.AuthorId);
        builder.Property(a => a.AuthorId).ValueGeneratedOnAdd();
        builder.Property(a => a.NormalizedName).IsRequired().HasMaxLength(512);
        builder.Property(a => a.SortName).HasMaxLength(512);

        // Normalized name index for lookup and deduplication.
        builder.HasIndex(a => a.NormalizedName)
            .HasDatabaseName("IX_Authors_NormalizedName");
    }
}
