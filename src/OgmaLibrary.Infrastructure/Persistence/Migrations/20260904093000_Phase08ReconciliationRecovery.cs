using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Adds temporal absence evidence for safe filesystem reconciliation.</summary>
[DbContext(typeof(CatalogueDbContext))]
[Migration("20260904093000_Phase08ReconciliationRecovery")]
public partial class Phase08ReconciliationRecovery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReconciliationReviews",
            columns: table => new
            {
                ReconciliationReviewId = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                FileOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                ReasonCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CandidatePathsJson = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                DecidedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReconciliationReviews", x => x.ReconciliationReviewId);
                table.CheckConstraint("CK_ReconciliationReviews_OccurrenceId", "length(FileOccurrenceId) = 26");
                table.CheckConstraint("CK_ReconciliationReviews_RootId", "length(LibraryRootId) = 26");
                table.CheckConstraint("CK_ReconciliationReviews_Status", "Status BETWEEN 0 AND 2");
                table.ForeignKey(
                    name: "FK_ReconciliationReviews_LibraryRoots_LibraryRootId",
                    column: x => x.LibraryRootId,
                    principalTable: "LibraryRoots",
                    principalColumn: "LibraryRootId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationReviews_Occurrence_Status",
            table: "ReconciliationReviews",
            columns: new[] { "LibraryRootId", "FileOccurrenceId", "Status" });

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "MissingSinceUtc",
            table: "FileOccurrences",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MissingSinceUtc",
            table: "FileOccurrences");

        migrationBuilder.DropTable(
            name: "ReconciliationReviews");
    }
}
