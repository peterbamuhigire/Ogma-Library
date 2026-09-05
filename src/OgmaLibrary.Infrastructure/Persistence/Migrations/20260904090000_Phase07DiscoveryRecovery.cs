using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Adds durable per-directory recovery state for incremental discovery.</summary>
[DbContext(typeof(CatalogueDbContext))]
[Migration("20260904090000_Phase07DiscoveryRecovery")]
public partial class Phase07DiscoveryRecovery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "LastScanSessionId",
            table: "DirectoryCheckpoints",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastStartedUtc",
            table: "DirectoryCheckpoints",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ResumeCursorRelativeDirectory",
            table: "DirectoryCheckpoints",
            type: "TEXT",
            maxLength: 4096,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ScanState",
            table: "DirectoryCheckpoints",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"DirectoryCheckpoints\" DROP COLUMN \"LastScanSessionId\";");
        migrationBuilder.Sql("ALTER TABLE \"DirectoryCheckpoints\" DROP COLUMN \"LastStartedUtc\";");
        migrationBuilder.Sql("ALTER TABLE \"DirectoryCheckpoints\" DROP COLUMN \"ResumeCursorRelativeDirectory\";");
        migrationBuilder.Sql("ALTER TABLE \"DirectoryCheckpoints\" DROP COLUMN \"ScanState\";");
    }
}
