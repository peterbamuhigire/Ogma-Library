using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Infrastructure.Catalogue;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations;

/// <summary>Separates paused jobs from the dead-letter status value.</summary>
[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CatalogueDbContext))]
[Migration("20260906060000_Phase17PausedJobStatus")]
public partial class Phase17PausedJobStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE Jobs SET Status = 6 WHERE Status = 5 AND JobType IN ('OcrJob', 'Enrich');");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE Jobs SET Status = 5 WHERE Status = 6 AND JobType IN ('OcrJob', 'Enrich');");
    }
}
