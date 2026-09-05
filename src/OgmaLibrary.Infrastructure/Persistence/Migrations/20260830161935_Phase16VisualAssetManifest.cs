using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16VisualAssetManifest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisualAssetManifests",
                columns: table => new
                {
                    BookId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Variant = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WidthPx = table.Column<int>(type: "INTEGER", nullable: false),
                    HeightPx = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    GenerationVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCustom = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualAssetManifests", x => new { x.BookId, x.Kind, x.Variant });
                    table.CheckConstraint("CK_VisualAssets_Dimensions", "WidthPx > 0 AND HeightPx > 0");
                    table.CheckConstraint("CK_VisualAssets_GenerationVersion", "GenerationVersion > 0");
                    table.CheckConstraint("CK_VisualAssets_Kind", "Kind BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_VisualAssets_Status", "Status BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_VisualAssetManifests_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisualAssets_Book_Kind_Status",
                table: "VisualAssetManifests",
                columns: new[] { "BookId", "Kind", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisualAssetManifests");
        }
    }
}
