using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Applies pending EF Core migrations to the catalogue database at startup.
/// Before applying any migration, the existing database file is copied to a
/// timestamped backup. If migration fails, the backup is restored and the
/// error is re-thrown (NFR-PROD-010, NFR-PROD-012).
/// </summary>
public sealed class CatalogueMigrator
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueMigrator"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context to migrate.</param>
    internal CatalogueMigrator(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueMigrator"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="serviceProvider">The service provider, used only to make DI constructor selection unambiguous.</param>
    [ActivatorUtilitiesConstructor]
    public CatalogueMigrator(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _contextFactory = contextFactory;
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
        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string? dbPath = GetDatabasePath(context);

        // In-memory databases (tests) need no backup.
        if (dbPath is null || dbPath == ":memory:")
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await EnsureModelTablesExistAsync(context, dbPath, cancellationToken).ConfigureAwait(false);
            await EnsureNonModelDatabaseObjectsAsync(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Check whether there are any pending migrations. If none, skip the backup.
        var pending = await context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!pending.Any())
        {
            await EnsureModelTablesExistAsync(context, dbPath, cancellationToken).ConfigureAwait(false);
            await EnsureNonModelDatabaseObjectsAsync(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        // --- Backup the existing DB file before touching it (NFR-PROD-010) ---
        string? backupPath = BackupDatabase(dbPath);
        bool dbExisted = backupPath is not null;

        try
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await EnsureModelTablesExistAsync(context, dbPath, cancellationToken).ConfigureAwait(false);
            await EnsureNonModelDatabaseObjectsAsync(context, cancellationToken).ConfigureAwait(false);
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

    private static async Task EnsureNonModelDatabaseObjectsAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE VIRTUAL TABLE IF NOT EXISTS SearchFts5
            USING fts5(
                ChunkText,
                content='SearchChunks',
                content_rowid='ChunkId',
                tokenize='unicode61 remove_diacritics 1'
            );
            """,
            cancellationToken).ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS SearchChunks_Fts_Insert
            AFTER INSERT ON SearchChunks
            BEGIN
                INSERT INTO SearchFts5(rowid, ChunkText)
                VALUES (new.ChunkId, new.ChunkText);
            END;
            """,
            cancellationToken).ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS SearchChunks_Fts_Delete
            AFTER DELETE ON SearchChunks
            BEGIN
                INSERT INTO SearchFts5(SearchFts5, rowid, ChunkText)
                VALUES ('delete', old.ChunkId, old.ChunkText);
            END;
            """,
            cancellationToken).ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS SearchChunks_Fts_Update
            AFTER UPDATE ON SearchChunks
            BEGIN
                INSERT INTO SearchFts5(SearchFts5, rowid, ChunkText)
                VALUES ('delete', old.ChunkId, old.ChunkText);
                INSERT INTO SearchFts5(rowid, ChunkText)
                VALUES (new.ChunkId, new.ChunkText);
            END;
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureModelTablesExistAsync(
        CatalogueDbContext context,
        string? dbPath,
        CancellationToken cancellationToken)
    {
        List<string> missingTables = await GetMissingModelTablesAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (missingTables.Count == 0)
        {
            return;
        }

        if (dbPath is not null && dbPath != ":memory:")
        {
            BackupDatabase(dbPath);
        }

        string createScript = context.Database.GenerateCreateScript();
        foreach (string statement in SplitSqlStatements(createScript))
        {
            string idempotentStatement = MakeCreateStatementIdempotent(statement);
            await context.Database.ExecuteSqlRawAsync(idempotentStatement, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<List<string>> GetMissingModelTablesAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        var tableNames = context.Model
            .GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Select(tableName => tableName!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (string tableName in tableNames)
        {
            if (!await TableExistsAsync(context, tableName, cancellationToken).ConfigureAwait(false))
            {
                missing.Add(tableName);
            }
        }

        return missing;
    }

    private static async Task<bool> TableExistsAsync(
        CatalogueDbContext context,
        string tableName,
        CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var connection = context.Database.GetDbConnection();
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
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
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

        if (statement.StartsWith("INSERT INTO \"HostModeSettings\"", StringComparison.OrdinalIgnoreCase))
        {
            return "INSERT OR IGNORE INTO \"HostModeSettings\"" + statement["INSERT INTO \"HostModeSettings\"".Length..];
        }

        return statement;
    }

    /// <summary>
    /// Returns the file-system path of the SQLite database, or <see langword="null"/>
    /// for in-memory databases.
    /// </summary>
    private static string? GetDatabasePath(CatalogueDbContext context)
    {
        string? connectionString = context.Database.GetConnectionString();
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

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;

        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
