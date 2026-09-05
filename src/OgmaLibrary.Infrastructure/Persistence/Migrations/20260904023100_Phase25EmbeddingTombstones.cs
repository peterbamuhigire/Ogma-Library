using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase25EmbeddingTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTombstoned",
                table: "EmbeddingVectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TombstonedUtc",
                table: "EmbeddingVectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingVectors_Tombstone_Model",
                table: "EmbeddingVectors",
                columns: new[] { "IsTombstoned", "ModelName", "ModelVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmbeddingVectors_Tombstone_Model",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "IsTombstoned",
                table: "EmbeddingVectors");

            migrationBuilder.DropColumn(
                name: "TombstonedUtc",
                table: "EmbeddingVectors");
        }
    }
}
