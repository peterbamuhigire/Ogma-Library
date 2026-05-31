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
            await EnsureModelTablesExistAsync(dbPath, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Check whether there are any pending migrations. If none, skip the backup.
        var pending = await _context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!pending.Any())
        {
            await EnsureModelTablesExistAsync(dbPath, cancellationToken).ConfigureAwait(false);
            return;
        }

        // --- Backup the existing DB file before touching it (NFR-PROD-010) ---
        string? backupPath = BackupDatabase(dbPath);
        bool dbExisted = backupPath is not null;

        try
        {
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await EnsureModelTablesExistAsync(dbPath, cancellationToken).ConfigureAwait(false);
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

    private async Task EnsureModelTablesExistAsync(
        string? dbPath,
        CancellationToken cancellationToken)
    {
        List<string> missingTables = await GetMissingModelTablesAsync(cancellationToken)
            .ConfigureAwait(false);
        if (missingTables.Count == 0)
        {
            return;
        }

        if (dbPath is not null && dbPath != ":memory:")
        {
            BackupDatabase(dbPath);
        }

        string createScript = _context.Database.GenerateCreateScript();
        foreach (string statement in SplitSqlStatements(createScript))
        {
            string idempotentStatement = MakeCreateStatementIdempotent(statement);
            await _context.Database.ExecuteSqlRawAsync(idempotentStatement, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<List<string>> GetMissingModelTablesAsync(CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        var tableNames = _context.Model
            .GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Select(tableName => tableName!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (string tableName in tableNames)
        {
            if (!await TableExistsAsync(tableName, cancellationToken).ConfigureAwait(false))
            {
                missing.Add(tableName);
            }
        }

        return missing;
    }

    private async Task<bool> TableExistsAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var connection = _context.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static string? BackupDatabase(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return null;
        }

        string timestamp = DateTimeOffset.UtcNow.ToString(
            "yyyyMMddHHmmss",
            System.Globalization.CultureInfo.InvariantCulture);
        string backupPath = $"{dbPath}.{timestamp}.bak";

        int suffix = 0;
        while (File.Exists(backupPath))
        {
            suffix++;
            backupPath = $"{dbPath}.{timestamp}.{suffix}.bak";
        }

        File.Copy(dbPath, backupPath, overwrite: false);
        return backupPath;
    }

    private static IEnumerable<string> SplitSqlStatements(string sql)
    {
        foreach (string statement in sql.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return statement;
        }
    }

    private static string MakeCreateStatementIdempotent(string statement)
    {
        if (statement.StartsWith("CREATE TABLE ", StringComparison.OrdinalIgnoreCase))
        {
            return "CREATE TABLE IF NOT EXISTS " + statement["CREATE TABLE ".Length..];
        }

        if (statement.StartsWith("CREATE UNIQUE INDEX ", StringComparison.OrdinalIgnoreCase))
        {
            return "CREATE UNIQUE INDEX IF NOT EXISTS " + statement["CREATE UNIQUE INDEX ".Length..];
        }

        if (statement.StartsWith("CREATE INDEX ", StringComparison.OrdinalIgnoreCase))
        {
            return "CREATE INDEX IF NOT EXISTS " + statement["CREATE INDEX ".Length..];
        }

        return statement;
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
