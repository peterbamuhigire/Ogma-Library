using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase25EmbeddingIndexLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion",
                table: "EmbeddingVectors");

            migrationBuilder.CreateTable(
                name: "EmbeddingIndexState",
                columns: table => new
                {
                    StateKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ActiveIndexVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StagingIndexVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingIndexState", x => x.StateKey);
                    table.CheckConstraint("CK_EmbeddingIndexState_ActiveVersion", "length(ActiveIndexVersion) BETWEEN 1 AND 128");
                    table.CheckConstraint("CK_EmbeddingIndexState_StagingVersion", "StagingIndexVersion IS NULL OR length(StagingIndexVersion) BETWEEN 1 AND 128");
                });

            migrationBuilder.CreateIndex(
                name: "UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion_IndexVersion",
                table: "EmbeddingVectors",
                columns: new[] { "ChunkId", "ModelName", "ModelVersion", "IndexVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmbeddingIndexState");

            migrationBuilder.DropIndex(
                name: "UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion_IndexVersion",
                table: "EmbeddingVectors");

            migrationBuilder.CreateIndex(
                name: "UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion",
                table: "EmbeddingVectors",
                columns: new[] { "ChunkId", "ModelName", "ModelVersion" },
                unique: true);
        }
    }
}
