using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase07DiscoveryObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryCheckpoints",
                columns: table => new
                {
                    DirectoryCheckpointId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    NormalizedRelativeDirectory = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    LastCompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastObservedFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryCheckpoints", x => x.DirectoryCheckpointId);
                    table.CheckConstraint("CK_DirectoryCheckpoints_Count", "LastObservedFileCount >= 0");
                    table.CheckConstraint("CK_DirectoryCheckpoints_RootId", "length(LibraryRootId) = 26");
                    table.ForeignKey(
                        name: "FK_DirectoryCheckpoints_LibraryRoots_LibraryRootId",
                        column: x => x.LibraryRootId,
                        principalTable: "LibraryRoots",
                        principalColumn: "LibraryRootId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveryObservations",
                columns: table => new
                {
                    DiscoveryObservationId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    NormalizedRelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryObservations", x => x.DiscoveryObservationId);
                    table.CheckConstraint("CK_DiscoveryObservations_Path", "length(NormalizedRelativePath) > 0");
                    table.CheckConstraint("CK_DiscoveryObservations_RootId", "length(LibraryRootId) = 26");
                    table.CheckConstraint("CK_DiscoveryObservations_Size", "SizeBytes >= 0");
                    table.ForeignKey(
                        name: "FK_DiscoveryObservations_LibraryRoots_LibraryRootId",
                        column: x => x.LibraryRootId,
                        principalTable: "LibraryRoots",
                        principalColumn: "LibraryRootId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_DirectoryCheckpoints_Root_Directory",
                table: "DirectoryCheckpoints",
                columns: new[] { "LibraryRootId", "NormalizedRelativeDirectory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_DiscoveryObservations_Root_Path",
                table: "DiscoveryObservations",
                columns: new[] { "LibraryRootId", "NormalizedRelativePath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryCheckpoints");

            migrationBuilder.DropTable(
                name: "DiscoveryObservations");
        }
    }
}
