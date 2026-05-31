using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>
/// EF Core entity type configuration for the BookAuthors join table (HLD §3).
/// </summary>
public sealed class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthorRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookAuthorRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BookAuthors");
        builder.HasKey(ba => new { ba.BookId, ba.AuthorId });
        builder.Property(ba => ba.BookId).IsRequired().HasMaxLength(26);
        builder.Property(ba => ba.AuthorId).IsRequired();
        builder.Property(ba => ba.Role).HasMaxLength(64);
        builder.Property(ba => ba.DisplayOrder).HasDefaultValue(0);

        builder.HasOne(ba => ba.Author)
            .WithMany(a => a.BookAuthors)
            .HasForeignKey(ba => ba.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
