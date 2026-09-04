using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase24OcrQualityProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSelectedText",
                table: "ExtractedPages",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<double>(
                name: "OcrConfidence",
                table: "ExtractedPages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrLanguage",
                table: "ExtractedPages",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrModelVersion",
                table: "ExtractedPages",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"ExtractedPages\" DROP COLUMN \"IsSelectedText\";");
            migrationBuilder.Sql("ALTER TABLE \"ExtractedPages\" DROP COLUMN \"OcrConfidence\";");
            migrationBuilder.Sql("ALTER TABLE \"ExtractedPages\" DROP COLUMN \"OcrLanguage\";");
            migrationBuilder.Sql("ALTER TABLE \"ExtractedPages\" DROP COLUMN \"OcrModelVersion\";");
        }
    }
}
