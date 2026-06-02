using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18SchoolAdminTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageLedger",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Date = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    QueryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    EstimatedCostUsd = table.Column<decimal>(type: "REAL", nullable: false, defaultValue: 0m),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLedger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrolledProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BirthYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EnrollmentToken = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EnrollmentTokenExpiresUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EnrolledUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrolledProfiles", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "LibraryPublishSettings",
                columns: table => new
                {
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SourcePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AiTier = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryPublishSettings", x => x.LibraryRootId);
                });

            migrationBuilder.CreateTable(
                name: "SchoolAiEntitlements",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    DailyTokenBudget = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 10000),
                    ClassDailyTokenBudget = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 500000),
                    RateLimitQueriesPerMin = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolAiEntitlements", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "SharedShelves",
                columns: table => new
                {
                    ShelfId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    GroupIdsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedShelves", x => x.ShelfId);
                });

            migrationBuilder.CreateTable(
                name: "SharedShelfBooks",
                columns: table => new
                {
                    ShelfId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    AddedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedShelfBooks", x => new { x.ShelfId, x.BookId });
                    table.ForeignKey(
                        name: "FK_SharedShelfBooks_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedShelfBooks_SharedShelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "SharedShelves",
                        principalColumn: "ShelfId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLedger_Date",
                table: "AiUsageLedger",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "UX_AiUsageLedger_ProfileId_Date",
                table: "AiUsageLedger",
                columns: new[] { "ProfileId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrolledProfiles_Role_RevokedUtc",
                table: "EnrolledProfiles",
                columns: new[] { "Role", "RevokedUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_EnrolledProfiles_EnrollmentToken",
                table: "EnrolledProfiles",
                column: "EnrollmentToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryPublishSettings_IsPublished",
                table: "LibraryPublishSettings",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "UX_LibraryPublishSettings_SourcePath",
                table: "LibraryPublishSettings",
                column: "SourcePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedShelfBooks_BookId",
                table: "SharedShelfBooks",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedShelves_Name",
                table: "SharedShelves",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SharedShelves_Visibility_IsDeleted",
                table: "SharedShelves",
                columns: new[] { "Visibility", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLedger");

            migrationBuilder.DropTable(
                name: "EnrolledProfiles");

            migrationBuilder.DropTable(
                name: "LibraryPublishSettings");

            migrationBuilder.DropTable(
                name: "SchoolAiEntitlements");

            migrationBuilder.DropTable(
                name: "SharedShelfBooks");

            migrationBuilder.DropTable(
                name: "SharedShelves");
        }
    }
}
