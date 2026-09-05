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

            // SQLite cannot remove a column that is referenced by the table's
            // foreign-key definition. Rebuild the pre-Phase-23 shape while
            // preserving the existing rows and the original page FK.
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;
                CREATE TABLE "SearchChunks_Down" (
                    "ChunkId" INTEGER NOT NULL CONSTRAINT "PK_SearchChunks" PRIMARY KEY AUTOINCREMENT,
                    "BookId" TEXT NOT NULL,
                    "ExtractedPageId" INTEGER NULL,
                    "ChunkIndex" INTEGER NOT NULL,
                    "ChunkText" TEXT NULL,
                    "TokenCount" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "Source" INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_SearchChunks_ExtractedPages_ExtractedPageId"
                        FOREIGN KEY ("ExtractedPageId") REFERENCES "ExtractedPages" ("ExtractedPageId") ON DELETE SET NULL
                );
                INSERT INTO "SearchChunks_Down"
                    ("ChunkId", "BookId", "ExtractedPageId", "ChunkIndex", "ChunkText", "TokenCount", "CreatedAtUtc", "Source")
                SELECT "ChunkId", "BookId", "ExtractedPageId", "ChunkIndex", "ChunkText", "TokenCount", "CreatedAtUtc", "Source"
                FROM "SearchChunks";
                DROP TABLE "SearchChunks";
                ALTER TABLE "SearchChunks_Down" RENAME TO "SearchChunks";
                CREATE INDEX "IX_SearchChunks_BookId_ChunkIndex"
                    ON "SearchChunks" ("BookId", "ChunkIndex");
                CREATE INDEX "IX_SearchChunks_BookId_Source"
                    ON "SearchChunks" ("BookId", "Source");
                CREATE INDEX "IX_SearchChunks_ExtractedPageId"
                    ON "SearchChunks" ("ExtractedPageId");
                PRAGMA foreign_keys = ON;
                """);
            migrationBuilder.Sql("ALTER TABLE \"ExtractedPages\" DROP COLUMN \"ExtractorVersion\";");
        }
    }
}
