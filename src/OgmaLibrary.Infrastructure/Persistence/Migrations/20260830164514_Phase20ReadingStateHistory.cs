using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase20ReadingStateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavourite",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ReadingStateHistory",
                columns: table => new
                {
                    ReadingStateHistoryId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReadingStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    IsFavourite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ChangedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingStateHistory", x => x.ReadingStateHistoryId);
                    table.CheckConstraint("CK_ReadingStateHistory_Rating", "Rating IS NULL OR Rating BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ReadingStateHistory_Status", "ReadingStatus IS NULL OR ReadingStatus BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ReadingStateHistory_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingStateHistory_Book_Changed",
                table: "ReadingStateHistory",
                columns: new[] { "BookId", "ChangedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingStateHistory");

            migrationBuilder.Sql("ALTER TABLE \"Books\" DROP COLUMN \"IsFavourite\";");
        }
    }
}
