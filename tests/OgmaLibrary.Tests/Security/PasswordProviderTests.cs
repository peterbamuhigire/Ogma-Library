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
    public async Task MacOsKeychainPasswordProvider_NonMac_ReturnsUnavailableWithoutPrompt()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX))
        {
            return;
        }

        var provider = new MacOsKeychainPasswordProvider();
        using PasswordResult result = await provider.GetPasswordAsync(
            new PasswordRequest("book-1", new string('e', 64), "Protected fixture"));

        Assert.Null(result.Password);
        Assert.Contains("macOS Keychain", result.ErrorMessage, StringComparison.Ordinal);
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

    [Fact]
    public async Task PasswordUnlock_OpenProtectedAsync_ClearsProviderBuffer()
    {
        char[] buffer = "ogma-test-password".ToCharArray();
        var provider = new CapturingPasswordProvider(buffer);
        var sessions = new CapturingReaderSessionService();
        var viewModel = new PasswordUnlockViewModel(provider, new InMemoryLocalizationService(), sessions);

        ReaderSession? session = await viewModel.RequestAndOpenAsync(
            "BOOK-PASSWORD-000000002",
            new string('c', 64),
            "Protected fixture");

        Assert.NotNull(session);
        Assert.Equal("ogma-test-password", new string(sessions.LastPassword!));
        Assert.All(buffer, ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public async Task PasswordUnlock_IncorrectPassword_ForgetsStoredCredential()
    {
        var provider = new CapturingPasswordProvider("wrong-password".ToCharArray());
        var sessions = new CapturingReaderSessionService { ThrowIncorrectPassword = true };
        var viewModel = new PasswordUnlockViewModel(provider, new InMemoryLocalizationService(), sessions);

        ReaderSession? session = await viewModel.RequestAndOpenAsync(
            "BOOK-PASSWORD-000000003",
            new string('d', 64),
            "Protected fixture");

        Assert.Null(session);
        Assert.True(provider.ForgetCalled);
        Assert.Equal("Password incorrect. Try again.", viewModel.StatusText);
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

    private sealed class CapturingPasswordProvider : IPasswordProvider
    {
        private readonly char[] _password;

        public CapturingPasswordProvider(char[] password) => _password = password;

        public bool ForgetCalled { get; private set; }

        public Task<PasswordResult> GetPasswordAsync(
            PasswordRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordResult.Success(_password, wasStored: false));

        public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default)
        {
            ForgetCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingReaderSessionService : IReaderSessionService
    {
        public ReaderSession? CurrentSession { get; private set; }

        public IPdfRenderer? CurrentRenderer => null;

        public char[]? LastPassword { get; private set; }

        public bool ThrowIncorrectPassword { get; init; }

        public Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct) =>
            OpenProtectedAsync(bookId, pageHint, [], ct);

        public Task<ReaderSession> OpenProtectedAsync(
            string bookId,
            int? pageHint,
            char[] password,
            CancellationToken ct)
        {
            if (ThrowIncorrectPassword)
            {
                throw new PdfPasswordIncorrectException("protected.pdf");
            }

            LastPassword = password.ToArray();
            CurrentSession = new ReaderSession(
                bookId,
                "protected.pdf",
                PageCount: 1,
                CurrentPageIndex: pageHint ?? 0,
                ScrollOffset: 0,
                ZoomMode.FitWidth,
                ZoomPercent: 100,
                DisplayMode.SinglePage);
            return Task.FromResult(CurrentSession);
        }

        public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;

        public Task NavigateToAsync(int pageIndex, double scrollOffset = 0) => Task.CompletedTask;

        public void UpdateScrollOffset(double scrollOffset)
        {
        }
    }
}
