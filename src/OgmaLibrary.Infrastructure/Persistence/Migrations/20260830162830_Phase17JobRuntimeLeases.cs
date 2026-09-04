using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase17JobRuntimeLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "Jobs",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresUtc",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "Jobs",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptUtc",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_DueWork",
                table: "Jobs",
                columns: new[] { "Status", "NextAttemptUtc", "JobType" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_LeaseExpiry",
                table: "Jobs",
                column: "LeaseExpiresUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_DueWork",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_LeaseExpiry",
                table: "Jobs");

            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"FailureCode\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"LeaseExpiresUtc\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"LeaseOwner\";");
            migrationBuilder.Sql("ALTER TABLE \"Jobs\" DROP COLUMN \"NextAttemptUtc\";");
        }
    }
}
