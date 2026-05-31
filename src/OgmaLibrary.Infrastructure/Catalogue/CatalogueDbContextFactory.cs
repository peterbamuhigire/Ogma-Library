using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Design-time factory for <see cref="CatalogueDbContext"/>, enabling <c>dotnet ef</c>
/// migrations tooling to operate without launching the Avalonia application
/// (ADR-0005 — EF Core migrations are the source-of-truth for the deployed schema).
/// </summary>
/// <remarks>
/// The factory creates a context pointing at a development SQLite file in the working
/// directory. The path is never used at runtime — the composition root provides the
/// real path from the user's app-data directory.
/// </remarks>
public sealed class CatalogueDbContextFactory : IDesignTimeDbContextFactory<CatalogueDbContext>
{
    /// <summary>
    /// Creates a <see cref="CatalogueDbContext"/> for design-time tooling.
    /// </summary>
    /// <param name="args">Unused; required by the interface contract.</param>
    /// <returns>A configured <see cref="CatalogueDbContext"/>.</returns>
    public CatalogueDbContext CreateDbContext(string[] args)
    {
        // dev.db is a throwaway SQLite file used only by dotnet-ef tooling.
        string dbPath = Path.Combine(
            AppContext.BaseDirectory,
            "dev-catalogue.db");

        var optionsBuilder = new DbContextOptionsBuilder<CatalogueDbContext>();
        optionsBuilder.UseSqlite(
            $"Data Source={dbPath}",
            sqlite => sqlite.MigrationsAssembly(typeof(CatalogueDbContext).Assembly.GetName().Name));

        return new CatalogueDbContext(optionsBuilder.Options);
    }
}
