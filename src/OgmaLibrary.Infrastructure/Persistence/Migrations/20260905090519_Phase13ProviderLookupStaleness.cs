using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13ProviderLookupStaleness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStale",
                table: "MetadataLookups",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The SQLite migrations SQL generator does not emit DropColumnOperation
            // for this provider. SQLite itself supports the operation on the
            // deployed runtime versions, so keep the rollback executable without
            // rebuilding the table or losing unrelated lookup data.
            migrationBuilder.Sql(
                "ALTER TABLE \"MetadataLookups\" DROP COLUMN \"IsStale\";");
        }
    }
}
