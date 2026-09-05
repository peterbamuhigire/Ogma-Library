using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase14MetadataReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetadataProposals",
                columns: table => new
                {
                    MetadataProposalId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProposedValue = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    CurrentValue = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AlternativesJson = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataProposals", x => x.MetadataProposalId);
                    table.CheckConstraint("CK_MetadataProposals_Alternatives", "length(AlternativesJson) <= 65536");
                    table.CheckConstraint("CK_MetadataProposals_Confidence", "Confidence BETWEEN 0.0 AND 1.0");
                    table.CheckConstraint("CK_MetadataProposals_Status", "Status BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_MetadataProposals_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataProposals_Book_Status_Created",
                table: "MetadataProposals",
                columns: new[] { "BookId", "Status", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataProposals");
        }
    }
}
