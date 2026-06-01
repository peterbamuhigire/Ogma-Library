using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10SearchIndexSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "SearchChunks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "SearchChunks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "ExtractedPages",
                type: "TEXT",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtractionQuality",
                table: "ExtractedPages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "WordCount",
                table: "ExtractedPages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IndexStatus",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SearchChunks_BookId_Source",
                table: "SearchChunks",
                columns: new[] { "BookId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_ContentHash",
                table: "ExtractedPages",
                columns: new[] { "BookId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages",
                columns: new[] { "BookId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_IndexStatus",
                table: "Books",
                column: "IndexStatus");

            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS SearchFts5
                USING fts5(
                    ChunkText,
                    content='SearchChunks',
                    content_rowid='ChunkId',
                    tokenize='unicode61 remove_diacritics 1'
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER IF NOT EXISTS SearchChunks_Fts_Insert
                AFTER INSERT ON SearchChunks
                BEGIN
                    INSERT INTO SearchFts5(rowid, ChunkText)
                    VALUES (new.ChunkId, new.ChunkText);
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER IF NOT EXISTS SearchChunks_Fts_Delete
                AFTER DELETE ON SearchChunks
                BEGIN
                    INSERT INTO SearchFts5(SearchFts5, rowid, ChunkText)
                    VALUES ('delete', old.ChunkId, old.ChunkText);
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER IF NOT EXISTS SearchChunks_Fts_Update
                AFTER UPDATE ON SearchChunks
                BEGIN
                    INSERT INTO SearchFts5(SearchFts5, rowid, ChunkText)
                    VALUES ('delete', old.ChunkId, old.ChunkText);
                    INSERT INTO SearchFts5(rowid, ChunkText)
                    VALUES (new.ChunkId, new.ChunkText);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SearchChunks_Fts_Update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SearchChunks_Fts_Delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SearchChunks_Fts_Insert;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS SearchFts5;");

            migrationBuilder.DropIndex(
                name: "IX_SearchChunks_BookId_Source",
                table: "SearchChunks");

            migrationBuilder.DropIndex(
                name: "IX_ExtractedPages_BookId_ContentHash",
                table: "ExtractedPages");

            migrationBuilder.DropIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages");

            migrationBuilder.DropIndex(
                name: "IX_Books_IndexStatus",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "SearchChunks");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "SearchChunks");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "ExtractedPages");

            migrationBuilder.DropColumn(
                name: "ExtractionQuality",
                table: "ExtractedPages");

            migrationBuilder.DropColumn(
                name: "WordCount",
                table: "ExtractedPages");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Books");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages",
                columns: new[] { "BookId", "PageNumber" });
        }
    }
}
