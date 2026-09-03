using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Stores provider validators for conditional cache revalidation.</summary>
[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CatalogueDbContext))]
[Migration("20260904123000_Phase13ProviderEtags")]
public partial class Phase13ProviderEtags : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ETag",
            table: "ProviderCacheEntries",
            type: "TEXT",
            maxLength: 512,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ETag", table: "ProviderCacheEntries");
    }
}
