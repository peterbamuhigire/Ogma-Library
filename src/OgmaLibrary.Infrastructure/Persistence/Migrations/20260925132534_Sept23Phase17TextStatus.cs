using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sept23Phase17TextStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OcrConfidence",
                table: "Books",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TextQuality",
                table: "Books",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TextStatus",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Books_TextStatus",
                table: "Books",
                column: "TextStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_TextStatus",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "OcrConfidence",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "TextQuality",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "TextStatus",
                table: "Books");
        }
    }
}
