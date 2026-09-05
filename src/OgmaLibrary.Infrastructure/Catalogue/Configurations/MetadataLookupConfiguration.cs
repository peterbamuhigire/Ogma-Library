using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the MetadataLookups table (HLD §3, FR-META-002).
/// </summary>
public sealed class MetadataLookupConfiguration : IEntityTypeConfiguration<MetadataLookupRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MetadataLookupRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MetadataLookups");
        builder.HasKey(l => l.LookupId);
        builder.Property(l => l.LookupId).ValueGeneratedOnAdd();
        builder.Property(l => l.BookId).IsRequired().HasMaxLength(26);
        builder.Property(l => l.Provider).IsRequired().HasMaxLength(64);
        builder.Property(l => l.RequestIsbn).HasMaxLength(13);
        builder.Property(l => l.ResponseJson);
        builder.Property(l => l.Timestamp);
        builder.Property(l => l.Confidence);
        builder.Property(l => l.Applied).HasDefaultValue(false);
        builder.Property(l => l.IsStale).HasDefaultValue(false);

        // Descending provider-timestamp index for recent lookups per book.
        builder.HasIndex(l => new { l.BookId, l.Provider, l.Timestamp })
            .HasDatabaseName("IX_MetadataLookups_BookId_Provider_Timestamp");
    }
}
