using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15SmartShelfIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ShelfBooks_ShelfId_BookId",
                table: "ShelfBooks",
                columns: new[] { "ShelfId", "BookId" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_Status_Year",
                table: "Books",
                columns: new[] { "Status", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMetadataFields_FieldName_Value",
                table: "BookMetadataFields",
                columns: new[] { "FieldName", "Value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShelfBooks_ShelfId_BookId",
                table: "ShelfBooks");

            migrationBuilder.DropIndex(
                name: "IX_Books_Status_Year",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookMetadataFields_FieldName_Value",
                table: "BookMetadataFields");
        }
    }
}
