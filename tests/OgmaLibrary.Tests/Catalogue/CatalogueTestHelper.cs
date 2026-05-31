using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Shared helpers for catalogue integration tests.
/// </summary>
internal static class CatalogueTestHelper
{
    /// <summary>
    /// Creates a <see cref="CatalogueDbContext"/> backed by a real temp SQLite file.
    /// The caller is responsible for disposing the context and calling
    /// <see cref="DeleteTempDb"/> after the test to release the file lock.
    /// </summary>
    public static (CatalogueDbContext Context, string DbPath) CreateTempFileContext()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-test-{Guid.NewGuid():N}.db");
        // Pooling=False: each test gets an independent connection with no shared pool, so
        // the global SqliteConnection.ClearAllPools() called by one test's cleanup cannot
        // disrupt a concurrently-open connection in another parallel test (which caused a
        // flaky OpenAsync failure under heavy parallel load).
        var options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False")
            .Options;
        var context = new CatalogueDbContext(options);
        return (context, dbPath);
    }

    /// <summary>
    /// Releases the SQLite connection pool for the given path and deletes the file.
    /// Must be called after disposing the context to avoid file-lock issues on Windows.
    /// </summary>
    public static void DeleteTempDb(string dbPath)
    {
        // Release all pooled connections to the SQLite file before deleting it.
        SqliteConnection.ClearAllPools();

        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        // Also delete any WAL or SHM files created by SQLite.
        string wal = dbPath + "-wal";
        string shm = dbPath + "-shm";
        if (File.Exists(wal))
        {
            File.Delete(wal);
        }

        if (File.Exists(shm))
        {
            File.Delete(shm);
        }
    }

    /// <summary>
    /// Creates a <see cref="CatalogueDbContext"/> backed by an in-memory SQLite database
    /// for tests that do not need a physical file.
    /// </summary>
    public static CatalogueDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new CatalogueDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
