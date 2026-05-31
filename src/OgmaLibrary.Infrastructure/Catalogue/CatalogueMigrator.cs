using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Applies pending EF Core migrations to the catalogue database at startup.
/// Before applying any migration, the existing database file is copied to a
/// timestamped backup. If migration fails, the backup is restored and the
/// error is re-thrown (NFR-PROD-010, NFR-PROD-012).
/// </summary>
public sealed class CatalogueMigrator
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueMigrator"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context to migrate.</param>
    public CatalogueMigrator(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Applies all pending migrations, taking a timestamped backup first.
    /// Restores the backup if migration fails (NFR-PROD-010).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the database path cannot be determined from the connection string.
    /// </exception>
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        string? dbPath = GetDatabasePath();

        // In-memory databases (tests) need no backup.
        if (dbPath is null || dbPath == ":memory:")
        {
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Check whether there are any pending migrations. If none, skip the backup.
        var pending = await _context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!pending.Any())
        {
            return;
        }

        // --- Backup the existing DB file before touching it (NFR-PROD-010) ---
        string? backupPath = null;
        bool dbExisted = File.Exists(dbPath);

        if (dbExisted)
        {
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            backupPath = $"{dbPath}.{timestamp}.bak";
            File.Copy(dbPath, backupPath, overwrite: false);
        }

        try
        {
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Restore the backup if migration failed.
            if (backupPath is not null && File.Exists(backupPath) && dbExisted)
            {
                try
                {
                    File.Copy(backupPath, dbPath, overwrite: true);
                }
                catch (IOException)
                {
                    // Best-effort restore; the original exception is the primary concern.
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Returns the file-system path of the SQLite database, or <see langword="null"/>
    /// for in-memory databases.
    /// </summary>
    private string? GetDatabasePath()
    {
        string? connectionString = _context.Database.GetConnectionString();
        if (connectionString is null)
        {
            return null;
        }

        // Parse "Data Source=<path>" from the connection string.
        foreach (string part in connectionString.Split(';'))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                string value = trimmed["Data Source=".Length..].Trim();
                return value.Length == 0 ? null : value;
            }
        }

        return null;
    }
}
