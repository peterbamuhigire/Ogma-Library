using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16LanHostTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostClientSessions",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IssuedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostClientSessions", x => x.TokenHash);
                });

            migrationBuilder.CreateTable(
                name: "HostModeSettings",
                columns: table => new
                {
                    SettingsId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentMode = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostModeSettings", x => x.SettingsId);
                });

            migrationBuilder.InsertData(
                table: "HostModeSettings",
                columns: new[] { "SettingsId", "ContentMode", "DisplayName", "IsEnabled", "Port", "UpdatedUtc" },
                values: new object[] { "default", 0, "Ogma Library", false, 7473, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_HostClientSessions_ClientId_ExpiresUtc",
                table: "HostClientSessions",
                columns: new[] { "ClientId", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HostClientSessions_RevokedUtc",
                table: "HostClientSessions",
                column: "RevokedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostClientSessions");

            migrationBuilder.DropTable(
                name: "HostModeSettings");
        }
    }
}
