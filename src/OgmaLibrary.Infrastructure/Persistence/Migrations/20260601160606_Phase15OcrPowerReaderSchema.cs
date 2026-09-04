using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15OcrPowerReaderSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ExtractedPages",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Extraction");

            migrationBuilder.AddColumn<bool>(
                name: "IsOcrDerived",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPasswordProtected",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages",
                columns: new[] { "BookId", "PageNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_Source_PageNumber",
                table: "ExtractedPages",
                columns: new[] { "BookId", "Source", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsOcrDerived",
                table: "Books",
                column: "IsOcrDerived");

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsPasswordProtected",
                table: "Books",
                column: "IsPasswordProtected");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages");

            migrationBuilder.DropIndex(
                name: "IX_ExtractedPages_BookId_Source_PageNumber",
                table: "ExtractedPages");

            migrationBuilder.DropIndex(
                name: "IX_Books_IsOcrDerived",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_IsPasswordProtected",
                table: "Books");

            migrationBuilder.Sql("ALTER TABLE \"ExtractedPages\" DROP COLUMN \"Source\";");
            migrationBuilder.Sql("ALTER TABLE \"Books\" DROP COLUMN \"IsOcrDerived\";");
            migrationBuilder.Sql("ALTER TABLE \"Books\" DROP COLUMN \"IsPasswordProtected\";");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages",
                columns: new[] { "BookId", "PageNumber" },
                unique: true);
        }
    }
}
