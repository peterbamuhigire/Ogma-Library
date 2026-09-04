using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase21AnnotationCoordinateVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CoordinateVersion",
            table: "AnnotationsV2",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "normalized-v1");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CoordinateVersion",
            table: "AnnotationsV2");
    }
}
