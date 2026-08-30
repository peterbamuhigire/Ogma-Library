using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase23SearchIndexVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExtractionArtifactId",
                table: "SearchChunks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexVersion",
                table: "SearchChunks",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "fts5-v1");

            migrationBuilder.AddColumn<string>(
                name: "ExtractorVersion",
                table: "ExtractedPages",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "pdf-text-v1");

            migrationBuilder.CreateIndex(
                name: "IX_SearchChunks_BookId_IndexVersion",
                table: "SearchChunks",
                columns: new[] { "BookId", "IndexVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchChunks_ExtractionArtifactId",
                table: "SearchChunks",
                column: "ExtractionArtifactId");

            migrationBuilder.AddForeignKey(
                name: "FK_SearchChunks_ExtractionArtifacts_ExtractionArtifactId",
                table: "SearchChunks",
                column: "ExtractionArtifactId",
                principalTable: "ExtractionArtifacts",
                principalColumn: "ExtractionArtifactId",
                onDelete: ReferentialAction.SetNull);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SearchChunks_ExtractionArtifacts_ExtractionArtifactId",
                table: "SearchChunks");

            migrationBuilder.DropIndex(
                name: "IX_SearchChunks_BookId_IndexVersion",
                table: "SearchChunks");

            migrationBuilder.DropIndex(
                name: "IX_SearchChunks_ExtractionArtifactId",
                table: "SearchChunks");

            migrationBuilder.DropColumn(
                name: "ExtractionArtifactId",
                table: "SearchChunks");

            migrationBuilder.DropColumn(
                name: "IndexVersion",
                table: "SearchChunks");

            migrationBuilder.DropColumn(
                name: "ExtractorVersion",
                table: "ExtractedPages");
        }
    }
}
