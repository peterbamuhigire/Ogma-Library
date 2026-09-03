using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Adds executable metadata scope and confidence-model versioning.</summary>
[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CatalogueDbContext))]
[Migration("20260904113000_Phase12MetadataScope")]
public partial class Phase12MetadataScope : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ConfidenceModelVersion",
            table: "MetadataProposals",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "confidence-v1");

        migrationBuilder.AddColumn<int>(
            name: "Scope",
            table: "MetadataProposals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ConfidenceModelVersion", table: "MetadataProposals");
        migrationBuilder.DropColumn(name: "Scope", table: "MetadataProposals");
    }
}
