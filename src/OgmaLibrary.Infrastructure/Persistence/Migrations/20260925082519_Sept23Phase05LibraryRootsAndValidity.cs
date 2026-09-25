using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sept23Phase05LibraryRootsAndValidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedUtc",
                table: "LibraryRoots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileValidity",
                table: "BookFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LibraryRootId",
                table: "BookFiles",
                type: "TEXT",
                maxLength: 26,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileIssues",
                columns: table => new
                {
                    FileIssueId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MtimeTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    IsIgnored = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DetectedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastCheckedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileIssues", x => x.FileIssueId);
                    table.CheckConstraint("CK_FileIssues_Reason", "Reason BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookFiles_LibraryRootId_RelativePath",
                table: "BookFiles",
                columns: new[] { "LibraryRootId", "RelativePath" });

            migrationBuilder.CreateIndex(
                name: "UX_FileIssues_LibraryRootId_RelativePath",
                table: "FileIssues",
                columns: new[] { "LibraryRootId", "RelativePath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileIssues");

            migrationBuilder.DropIndex(
                name: "IX_BookFiles_LibraryRootId_RelativePath",
                table: "BookFiles");

            // The SQLite migrations SQL generator does not emit DropColumnOperation;
            // SQLite itself supports DROP COLUMN on the deployed runtime versions
            // (same approach as Phase13ProviderLookupStaleness).
            migrationBuilder.Sql("ALTER TABLE \"LibraryRoots\" DROP COLUMN \"RemovedUtc\";");
            migrationBuilder.Sql("ALTER TABLE \"BookFiles\" DROP COLUMN \"FileValidity\";");
            migrationBuilder.Sql("ALTER TABLE \"BookFiles\" DROP COLUMN \"LibraryRootId\";");
        }
    }
}
