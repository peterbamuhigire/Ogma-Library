using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase23SearchRebuildCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchRebuildCheckpoints",
                columns: table => new
                {
                    SearchRebuildCheckpointId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RebuildId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    BooksAttempted = table.Column<int>(type: "INTEGER", nullable: false),
                    BooksIndexed = table.Column<int>(type: "INTEGER", nullable: false),
                    BooksFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunksWritten = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchRebuildCheckpoints", x => x.SearchRebuildCheckpointId);
                    table.CheckConstraint("CK_SearchRebuildCheckpoints_Counts", "BooksAttempted >= 0 AND BooksIndexed >= 0 AND BooksFailed >= 0 AND ChunksWritten >= 0");
                    table.CheckConstraint("CK_SearchRebuildCheckpoints_Status", "Status BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchRebuildCheckpoints_RebuildId",
                table: "SearchRebuildCheckpoints",
                column: "RebuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchRebuildCheckpoints_Status_UpdatedUtc",
                table: "SearchRebuildCheckpoints",
                columns: new[] { "Status", "UpdatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchRebuildCheckpoints");
        }
    }
}
