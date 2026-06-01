using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11EmbeddingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmbeddingVectors_ChunkId_ModelId",
                table: "EmbeddingVectors");

            migrationBuilder.RenameColumn(
                name: "ModelId",
                table: "EmbeddingVectors",
                newName: "ModelName");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "EmbeddingVectors",
                newName: "GeneratedAtUtc");

            migrationBuilder.AlterColumn<byte[]>(
                name: "VectorBlob",
                table: "EmbeddingVectors",
                type: "BLOB",
                nullable: false,
                defaultValue: Array.Empty<byte>(),
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "EmbeddingVectors",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingStatus",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion",
                table: "EmbeddingVectors",
                columns: new[] { "ChunkId", "ModelName", "ModelVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_EmbeddingStatus",
                table: "Books",
                column: "EmbeddingStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion",
                table: "EmbeddingVectors");

            migrationBuilder.DropIndex(
                name: "IX_Books_EmbeddingStatus",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "Books");

            migrationBuilder.RenameColumn(
                name: "ModelName",
                table: "EmbeddingVectors",
                newName: "ModelId");

            migrationBuilder.RenameColumn(
                name: "GeneratedAtUtc",
                table: "EmbeddingVectors",
                newName: "CreatedUtc");

            migrationBuilder.AlterColumn<byte[]>(
                name: "VectorBlob",
                table: "EmbeddingVectors",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingVectors_ChunkId_ModelId",
                table: "EmbeddingVectors",
                columns: new[] { "ChunkId", "ModelId" });
        }
    }
}
