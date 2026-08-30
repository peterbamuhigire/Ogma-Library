using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13ProviderCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderCacheEntries",
                columns: table => new
                {
                    ProviderCacheEntryId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    QueryKey = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ResponseJson = table.Column<string>(type: "TEXT", maxLength: 262144, nullable: false),
                    IsNegative = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetrievedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ContractVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCacheEntries", x => x.ProviderCacheEntryId);
                    table.CheckConstraint("CK_ProviderCacheEntries_Contract", "ContractVersion > 0");
                    table.CheckConstraint("CK_ProviderCacheEntries_Response", "length(ResponseJson) <= 262144");
                });

            migrationBuilder.CreateIndex(
                name: "UX_ProviderCacheEntries_Provider_Query",
                table: "ProviderCacheEntries",
                columns: new[] { "Provider", "QueryKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderCacheEntries");
        }
    }
}
