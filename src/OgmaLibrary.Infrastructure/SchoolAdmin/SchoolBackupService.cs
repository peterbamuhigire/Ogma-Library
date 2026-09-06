using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using OgmaLibrary.Application.SchoolAdmin;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>
/// Creates consistent online catalogue backups and performs non-destructive
/// restore rehearsals. Backup files contain school data and must be stored in
/// an administrator-controlled protected location.
/// </summary>
internal sealed class SchoolBackupService : ISchoolBackupService, IDisposable
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SchoolBackupService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _databasePath = Path.Combine(Path.GetFullPath(dataDirectory), "catalogue.db");
    }

    public async Task<SchoolBackupResult> CreateBackupAsync(
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_databasePath))
            {
                throw new FileNotFoundException("The school catalogue database does not exist.", _databasePath);
            }

            string directory = Path.GetFullPath(destinationDirectory);
            Directory.CreateDirectory(directory);
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            string backupPath = Path.Combine(directory, $"ogma-school-backup-{timestamp}-{Guid.NewGuid():N}.db");
            string temporaryPath = backupPath + ".tmp";
            try
            {
                await Task.Run(
                    () => CopyDatabase(_databasePath, temporaryPath),
                    cancellationToken).ConfigureAwait(false);
                VerifyIntegrity(temporaryPath);
                File.Move(temporaryPath, backupPath);

                var info = new FileInfo(backupPath);
                return new SchoolBackupResult(
                    backupPath,
                    await ComputeSha256Async(backupPath, cancellationToken).ConfigureAwait(false),
                    info.Length,
                    DateTimeOffset.UtcNow);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SchoolRestoreRehearsalResult> RehearseRestoreAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        string sourcePath = Path.GetFullPath(backupPath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string rehearsalDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-school-restore-{Guid.NewGuid():N}");
        string restoredPath = Path.Combine(rehearsalDirectory, "catalogue-restored.db");
        try
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("The school backup does not exist.", sourcePath);
            }

            Directory.CreateDirectory(rehearsalDirectory);
            await Task.Run(
                () => CopyDatabase(sourcePath, restoredPath),
                cancellationToken).ConfigureAwait(false);
            VerifyIntegrity(sourcePath);
            VerifyIntegrity(restoredPath);

            DatabaseFingerprint expected = ReadFingerprint(sourcePath);
            DatabaseFingerprint actual = ReadFingerprint(restoredPath);
            if (!string.Equals(expected.SchemaSha256, actual.SchemaSha256, StringComparison.Ordinal) ||
                !expected.TableRows.SequenceEqual(actual.TableRows))
            {
                throw new InvalidDataException("The restored school catalogue does not match the backup.");
            }

            return new SchoolRestoreRehearsalResult(
                await ComputeSha256Async(sourcePath, cancellationToken).ConfigureAwait(false),
                actual.SchemaSha256,
                actual.TableRows.Count,
                actual.TableRows.Sum(row => row.Value),
                DateTimeOffset.UtcNow);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(rehearsalDirectory))
            {
                Directory.Delete(rehearsalDirectory, recursive: true);
            }

            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private static void CopyDatabase(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly;Pooling=False");
        using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void VerifyIntegrity(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        string result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The school catalogue failed SQLite integrity verification.");
        }
    }

    private static DatabaseFingerprint ReadFingerprint(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText =
            "SELECT name, COALESCE(sql, '') FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        using SqliteDataReader reader = schemaCommand.ExecuteReader();
        var schema = new StringBuilder();
        var tableNames = new List<string>();
        while (reader.Read())
        {
            string tableName = reader.GetString(0);
            schema.Append(tableName).Append('\n').Append(reader.GetString(1)).Append('\n');
            if (!tableName.StartsWith("sqlite_", StringComparison.Ordinal))
            {
                tableNames.Add(tableName);
            }
        }

        var rows = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (string tableName in tableNames)
        {
            using SqliteCommand countCommand = connection.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM \"{tableName.Replace("\"", "\"\"", StringComparison.Ordinal)}\";";
            rows[tableName] = Convert.ToInt64(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        string schemaSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema.ToString())))
            .ToLowerInvariant();
        return new DatabaseFingerprint(schemaSha256, rows);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private sealed record DatabaseFingerprint(
        string SchemaSha256,
        IReadOnlyDictionary<string, long> TableRows);
}
