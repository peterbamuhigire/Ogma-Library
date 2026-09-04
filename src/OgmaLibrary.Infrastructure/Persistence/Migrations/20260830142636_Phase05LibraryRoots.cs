using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase05LibraryRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowSymlinkTraversal",
                table: "LibraryRoots",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalLocator",
                table: "LibraryRoots",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "LibraryRoots",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHealthCheckUtc",
                table: "LibraryRoots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulScanUtc",
                table: "LibraryRoots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PermissionStatus",
                table: "LibraryRoots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VolumeIdentity",
                table: "LibraryRoots",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_LibraryRoots_CanonicalLocator",
                table: "LibraryRoots",
                column: "CanonicalLocator",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LibraryRoots_CanonicalLocator",
                table: "LibraryRoots");

            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"AllowSymlinkTraversal\";");
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"CanonicalLocator\";");
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"IsEnabled\";");
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"LastHealthCheckUtc\";");
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"LastSuccessfulScanUtc\";");
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"PermissionStatus\";");
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"VolumeIdentity\";");
        }
    }
}
