using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Adds durable ranked ISBN evidence for versioned extraction artifacts.</summary>
[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CatalogueDbContext))]
[Migration("20260904103000_Phase11IsbnEvidence")]
public partial class Phase11IsbnEvidence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExtractedIsbnEvidence",
            columns: table => new
            {
                ExtractedIsbnEvidenceId = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BookId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                ExtractionArtifactId = table.Column<long>(type: "INTEGER", nullable: false),
                IsbnNormalized = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                IdentifierKind = table.Column<int>(type: "INTEGER", nullable: false),
                Source = table.Column<int>(type: "INTEGER", nullable: false),
                Rank = table.Column<int>(type: "INTEGER", nullable: false),
                IsBest = table.Column<bool>(type: "INTEGER", nullable: false),
                DetectedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExtractedIsbnEvidence", x => x.ExtractedIsbnEvidenceId);
                table.CheckConstraint("CK_ExtractedIsbnEvidence_Isbn", "length(IsbnNormalized) IN (10, 13)");
                table.CheckConstraint("CK_ExtractedIsbnEvidence_Kind", "IdentifierKind IN (0, 1)");
                table.CheckConstraint("CK_ExtractedIsbnEvidence_Rank", "Rank >= 0");
                table.CheckConstraint("CK_ExtractedIsbnEvidence_Source", "Source BETWEEN 0 AND 3");
                table.ForeignKey(
                    name: "FK_ExtractedIsbnEvidence_Books_BookId",
                    column: x => x.BookId,
                    principalTable: "Books",
                    principalColumn: "BookId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ExtractedIsbnEvidence_ExtractionArtifacts_ExtractionArtifactId",
                    column: x => x.ExtractionArtifactId,
                    principalTable: "ExtractionArtifacts",
                    principalColumn: "ExtractionArtifactId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExtractedIsbnEvidence_Book_Value",
            table: "ExtractedIsbnEvidence",
            columns: new[] { "BookId", "IsbnNormalized" });

        migrationBuilder.CreateIndex(
            name: "UX_ExtractedIsbnEvidence_Artifact_Value_Source",
            table: "ExtractedIsbnEvidence",
            columns: new[] { "ExtractionArtifactId", "IsbnNormalized", "Source" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ExtractedIsbnEvidence");
}
