using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11ExtractionArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionArtifacts",
                columns: table => new
                {
                    ExtractionArtifactId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ExtractorVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PagesProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedPages = table.Column<int>(type: "INTEGER", nullable: false),
                    ManifestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionArtifacts", x => x.ExtractionArtifactId);
                    table.CheckConstraint("CK_ExtractionArtifacts_Manifest", "ManifestHash IS NULL OR length(ManifestHash) = 64");
                    table.CheckConstraint("CK_ExtractionArtifacts_Pages", "PagesProcessed >= 0 AND FailedPages >= 0");
                    table.CheckConstraint("CK_ExtractionArtifacts_Status", "Status BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ExtractionArtifacts_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ExtractionArtifacts_Book_Content_Version",
                table: "ExtractionArtifacts",
                columns: new[] { "BookId", "ContentHash", "ExtractorVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractionArtifacts");
        }
    }
}
