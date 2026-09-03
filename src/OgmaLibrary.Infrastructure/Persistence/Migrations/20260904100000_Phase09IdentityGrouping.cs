using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Adds audited, reversible identity grouping state.</summary>
[DbContext(typeof(CatalogueDbContext))]
[Migration("20260904100000_Phase09IdentityGrouping")]
public partial class Phase09IdentityGrouping : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IdentityGroups",
            columns: table => new
            {
                IdentityGroupId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdentityGroups", x => x.IdentityGroupId);
                table.CheckConstraint("CK_IdentityGroups_Id", "length(IdentityGroupId) = 26");
                table.CheckConstraint("CK_IdentityGroups_Kind", "Kind BETWEEN 0 AND 1");
                table.CheckConstraint("CK_IdentityGroups_Version", "Version > 0");
            });

        migrationBuilder.CreateTable(
            name: "IdentityGroupMembers",
            columns: table => new
            {
                IdentityGroupId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                FileOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdentityGroupMembers", x => new { x.IdentityGroupId, x.FileOccurrenceId });
                table.CheckConstraint("CK_IdentityGroupMembers_GroupId", "length(IdentityGroupId) = 26");
                table.CheckConstraint("CK_IdentityGroupMembers_OccurrenceId", "length(FileOccurrenceId) = 26");
                table.ForeignKey("FK_IdentityGroupMembers_IdentityGroups_IdentityGroupId", x => x.IdentityGroupId, "IdentityGroups", "IdentityGroupId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "IdentityGroupChanges",
            columns: table => new
            {
                IdentityGroupChangeId = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                IdentityGroupId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                Operation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                BeforeMembersJson = table.Column<string>(type: "TEXT", maxLength: 262144, nullable: false),
                AfterMembersJson = table.Column<string>(type: "TEXT", maxLength: 262144, nullable: false),
                Actor = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdentityGroupChanges", x => x.IdentityGroupChangeId);
                table.ForeignKey("FK_IdentityGroupChanges_IdentityGroups_IdentityGroupId", x => x.IdentityGroupId, "IdentityGroups", "IdentityGroupId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_IdentityGroupMembers_Occurrence_Active", "IdentityGroupMembers", new[] { "FileOccurrenceId", "IsActive" });
        migrationBuilder.CreateIndex("IX_IdentityGroupChanges_Group_Order", "IdentityGroupChanges", new[] { "IdentityGroupId", "IdentityGroupChangeId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IdentityGroupChanges");
        migrationBuilder.DropTable(name: "IdentityGroupMembers");
        migrationBuilder.DropTable(name: "IdentityGroups");
    }
}
