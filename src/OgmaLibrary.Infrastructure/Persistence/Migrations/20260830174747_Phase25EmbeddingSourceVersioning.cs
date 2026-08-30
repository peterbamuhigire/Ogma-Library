using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase25EmbeddingSourceVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChunkerVersion",
                table: "EmbeddingVectors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "chunker-v1");

            migrationBuilder.AddColumn<string>(
                name: "ExtractorVersion",
                table: "EmbeddingVectors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "IndexVersion",
                table: "EmbeddingVectors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "fts5-v1");

            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "EmbeddingVectors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "ollama");

            migrationBuilder.AddColumn<string>(
                name: "SourceHash",
                table: "EmbeddingVectors",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkerVersion",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "ExtractorVersion",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "IndexVersion",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "ProviderKey",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "SourceHash",
                table: "EmbeddingVectors");
        }
    }
}
