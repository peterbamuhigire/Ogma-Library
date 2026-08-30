using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>
/// Restores external-content FTS triggers after SQLite has rebuilt the
/// SearchChunks table for the Phase 23 foreign key.
/// </summary>
[Migration("20260830170619_Phase23RestoreFtsTriggers")]
[DbContext(typeof(CatalogueDbContext))]
public partial class Phase23RestoreFtsTriggers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
        migrationBuilder.Sql("INSERT INTO SearchFts5(SearchFts5) VALUES ('rebuild');");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS SearchChunks_Fts_Update;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS SearchChunks_Fts_Delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS SearchChunks_Fts_Insert;");
    }
}
