using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase06ProcessingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScanSessions",
                columns: table => new
                {
                    ScanSessionId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StartedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanSessions", x => x.ScanSessionId);
                    table.CheckConstraint("CK_ScanSessions_RootId", "length(LibraryRootId) = 26");
                    table.CheckConstraint("CK_ScanSessions_Status", "Status BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ScanSessions_LibraryRoots_LibraryRootId",
                        column: x => x.LibraryRootId,
                        principalTable: "LibraryRoots",
                        principalColumn: "LibraryRootId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StageExecutions",
                columns: table => new
                {
                    StageExecutionId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScanSessionId = table.Column<long>(type: "INTEGER", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SubjectKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempt = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaseOwner = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LeaseExpiresUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextAttemptUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageExecutions", x => x.StageExecutionId);
                    table.CheckConstraint("CK_StageExecutions_Attempt", "Attempt >= 0");
                    table.CheckConstraint("CK_StageExecutions_Status", "Status BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_StageExecutions_ScanSessions_ScanSessionId",
                        column: x => x.ScanSessionId,
                        principalTable: "ScanSessions",
                        principalColumn: "ScanSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanSessions_Root_Status",
                table: "ScanSessions",
                columns: new[] { "LibraryRootId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_Claim",
                table: "StageExecutions",
                columns: new[] { "StageName", "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_StageExecutions_Idempotency",
                table: "StageExecutions",
                columns: new[] { "ScanSessionId", "StageName", "SubjectKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageExecutions");

            migrationBuilder.DropTable(
                name: "ScanSessions");
        }
    }
}
