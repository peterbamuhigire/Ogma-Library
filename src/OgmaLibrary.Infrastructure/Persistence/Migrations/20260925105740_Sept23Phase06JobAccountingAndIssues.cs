using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sept23Phase06JobAccountingAndIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeaseOwnerPid",
                table: "Jobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LeaseOwnerStartTicks",
                table: "Jobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequeueCount",
                table: "Jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WaitingCapability",
                table: "Jobs",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExtractionIssues",
                columns: table => new
                {
                    ExtractionIssueId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    PageIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, defaultValue: ""),
                    Code = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Occurrences = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    LastMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionIssues", x => x.ExtractionIssueId);
                    table.CheckConstraint("CK_ExtractionIssues_Occurrences", "Occurrences >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "UX_ExtractionIssues_Book_Hash_Page",
                table: "ExtractionIssues",
                columns: new[] { "BookId", "ContentHash", "PageIndex" },
                unique: true);

            // T06.4: page/book extraction failure records were stored as "ExtractionFailed"
            // job rows whose RetryCount grew per failed page. Move them to ExtractionIssues.
            migrationBuilder.Sql(
                "INSERT OR IGNORE INTO ExtractionIssues " +
                "(BookId, PageIndex, ContentHash, Code, Occurrences, LastMessage, FirstSeenUtc, LastSeenUtc) " +
                "SELECT j.BookId, CAST(json_extract(j.Payload, '$.pageIndex') AS INTEGER), " +
                "COALESCE(b.Sha256Hash, ''), " +
                "CASE WHEN json_extract(j.Payload, '$.pageIndex') IS NULL THEN 'search_book_extraction_failed' " +
                "ELSE 'search_page_extraction_failed' END, " +
                "MAX(j.RetryCount + 1, 1), j.ErrorMessage, " +
                "COALESCE(j.CompletedUtc, '1970-01-01 00:00:00+00:00'), " +
                "COALESCE(j.CompletedUtc, '1970-01-01 00:00:00+00:00') " +
                "FROM Jobs j LEFT JOIN Books b ON b.BookId = j.BookId " +
                "WHERE j.JobType = 'ExtractionFailed' AND j.BookId IS NOT NULL AND json_valid(COALESCE(j.Payload, '{}'));");
            migrationBuilder.Sql("DELETE FROM Jobs WHERE JobType = 'ExtractionFailed';");

            // T06.1: embedding jobs that "failed" only because no provider exists wait for it.
            migrationBuilder.Sql(
                "UPDATE Jobs SET Status = 7, WaitingCapability = 'semantic-embeddings', RetryCount = 0, " +
                "NextAttemptUtc = NULL, CompletedUtc = NULL, LeaseOwner = NULL, LeaseExpiresUtc = NULL " +
                "WHERE JobType IN ('EmbeddingJob', 'EmbeddingGeneration') " +
                "AND FailureCode = 'embedding_provider_unavailable' AND Status IN (0, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Jobs SET Status = 3, CompletedUtc = COALESCE(CompletedUtc, StartedUtc) " +
                "WHERE Status = 7;");

            migrationBuilder.DropTable(
                name: "ExtractionIssues");

            // The SQLite migrations SQL generator does not emit DropColumnOperation; SQLite
            // supports DROP COLUMN on the deployed runtime versions (as in Sept23Phase05).
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"LeaseOwnerPid\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"LeaseOwnerStartTicks\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"RequeueCount\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"WaitingCapability\";");
        }
    }
}
