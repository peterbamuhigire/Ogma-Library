using Microsoft.Data.Sqlite;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Security;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Security;

/// <summary>Phase 15 password-provider security tests.</summary>
public sealed class PasswordProviderTests
{
    [Fact]
    public void PasswordProvider_CredentialKey_Format_IsCorrect()
    {
        string hash = new('A', 64);

        string key = PasswordCredentialKey.Create(hash);

        Assert.Equal("Ogma:BookPassword:" + new string('a', 64), key);
        Assert.Throws<ArgumentException>(() => PasswordCredentialKey.Create("not-a-sha"));
    }

    [Fact]
    public void PasswordResult_Dispose_ClearsPasswordBuffer()
    {
        char[] buffer = "test-secret-42".ToCharArray();
        using PasswordResult result = PasswordResult.Success(buffer, wasStored: false);

        result.Dispose();

        Assert.All(buffer, ch => Assert.Equal('\0', ch));
        Assert.Null(result.Password);
    }

    [Fact]
    public async Task Password_NeverStoredInCatalogue()
    {
        const string secret = "test-secret-42";
        var (context, dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            context.Database.Migrate();
            context.Books.Add(new BookRow
            {
                BookId = "BOOK-PASSWORD-000000001",
                Title = "Protected fixture",
                Status = 0,
                Sha256Hash = new string('b', 64),
                IsPasswordProtected = true,
            });
            await context.SaveChangesAsync();

            var provider = new FixedPasswordProvider(secret);
            var viewModel = new PasswordUnlockViewModel(provider, new InMemoryLocalizationService());
            using PasswordResult result = await viewModel.RequestUnlockAsync(
                "BOOK-PASSWORD-000000001",
                new string('b', 64),
                "Protected fixture");

            Assert.NotNull(result.Password);
            Assert.Equal("Book unlocked", viewModel.StatusText);
            Assert.Equal(0, CountCatalogueSecretOccurrences(dbPath, secret));
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    private static int CountCatalogueSecretOccurrences(string dbPath, string secret)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using SqliteCommand tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tables = new List<string>();
        using (SqliteDataReader tablesReader = tablesCommand.ExecuteReader())
        {
            while (tablesReader.Read())
            {
                tables.Add(tablesReader.GetString(0));
            }
        }

        int count = 0;
        foreach (string table in tables)
        {
            foreach (string column in GetTextColumns(connection, table))
            {
                using SqliteCommand countCommand = connection.CreateCommand();
                countCommand.CommandText = $"SELECT COUNT(*) FROM \"{table}\" WHERE instr(\"{column}\", $secret) > 0";
                countCommand.Parameters.AddWithValue("$secret", secret);
                count += Convert.ToInt32(countCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return count;
    }

    private static List<string> GetTextColumns(SqliteConnection connection, string table)
    {
        var columns = new List<string>();
        using SqliteCommand columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = $"PRAGMA table_info(\"{table}\")";
        using SqliteDataReader columnsReader = columnsCommand.ExecuteReader();
        while (columnsReader.Read())
        {
            string type = columnsReader.GetString(2);
            if (type.Contains("TEXT", StringComparison.OrdinalIgnoreCase))
            {
                columns.Add(columnsReader.GetString(1));
            }
        }

        return columns;
    }

    private sealed class FixedPasswordProvider : IPasswordProvider
    {
        private readonly string _password;

        public FixedPasswordProvider(string password) => _password = password;

        public Task<PasswordResult> GetPasswordAsync(
            PasswordRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordResult.Success(_password.ToCharArray(), wasStored: false));

        public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
