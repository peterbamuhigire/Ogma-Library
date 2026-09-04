using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase12AiGatewayTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoryId",
                table: "AiQueryHistory",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QueryType",
                table: "AiQueryHistory",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE AiQueryHistory SET HistoryId = 'legacy-' || QueryId WHERE HistoryId = ''");

            migrationBuilder.Sql(
                "UPDATE AiQueryHistory SET QueryType = 'legacy' WHERE QueryType = ''");

            migrationBuilder.CreateTable(
                name: "AiAuditEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    PromptCacheTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "REAL", nullable: true),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResponseHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    QueryHistoryEntryId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiConsentRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AiQueryHistory_HistoryId",
                table: "AiQueryHistory",
                column: "HistoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_OccurredAt",
                table: "AiAuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_QueryHistoryEntryId",
                table: "AiAuditEvents",
                column: "QueryHistoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AiConsentRecords_Tier_Provider_Scope",
                table: "AiConsentRecords",
                columns: new[] { "Tier", "Provider", "Scope" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAuditEvents");

            migrationBuilder.DropTable(
                name: "AiConsentRecords");

            migrationBuilder.DropIndex(
                name: "UX_AiQueryHistory_HistoryId",
                table: "AiQueryHistory");

            migrationBuilder.Sql("ALTER TABLE \"AiQueryHistory\" DROP COLUMN \"HistoryId\";");
            migrationBuilder.Sql("ALTER TABLE \"AiQueryHistory\" DROP COLUMN \"QueryType\";");
        }
    }
}
