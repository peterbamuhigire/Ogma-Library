using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Adds quality/count fields for versioned PDF outline extraction.</summary>
[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CatalogueDbContext))]
[Migration("20260904110000_Phase11TocQuality")]
public partial class Phase11TocQuality : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TocEntries",
            table: "ExtractionArtifacts",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "TocQuality",
            table: "ExtractionArtifacts",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE ExtractionArtifacts DROP COLUMN TocEntries;");
        migrationBuilder.Sql("ALTER TABLE ExtractionArtifacts DROP COLUMN TocQuality;");
    }
}
