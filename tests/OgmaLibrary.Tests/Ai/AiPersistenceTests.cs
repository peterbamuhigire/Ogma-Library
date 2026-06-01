using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 AI persistence tests.</summary>
public sealed class AiPersistenceTests : IDisposable
{
    private const string Phase11Migration = "20260601104115_Phase11EmbeddingSchema";

    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public AiPersistenceTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task ConsentRepository_UpsertAndRevoke()
    {
        var repository = new AiConsentRepository(_context);
        var consent = new AiConsentRecord(
            "consent-1",
            AiPrivacyTier.MetadataOnly,
            "anthropic",
            "library:default",
            DateTimeOffset.UtcNow);

        await repository.UpsertAsync(consent, CancellationToken.None);
        AiConsentRecord? active = await repository.GetActiveConsentAsync(
            AiPrivacyTier.MetadataOnly,
            "anthropic",
            "library:default",
            CancellationToken.None);
        int revoked = await repository.RevokeAllAsync(
            AiPrivacyTier.MetadataOnly,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        AiConsentRecord? afterRevoke = await repository.GetActiveConsentAsync(
            AiPrivacyTier.MetadataOnly,
            "anthropic",
            "library:default",
            CancellationToken.None);

        Assert.NotNull(active);
        Assert.Equal("consent-1", active.Id);
        Assert.Equal(1, revoked);
        Assert.Null(afterRevoke);
    }

    [Fact]
    public async Task AuditRepository_AppendIsImmutableAndExportsJson()
    {
        var repository = new AiAuditRepository(_context);
        var audit = new AiAuditEvent(
            "audit-1",
            DateTimeOffset.UtcNow,
            AiPrivacyTier.MetadataOnly,
            "openai",
            "gpt-test",
            new string('a', 64),
            new string('b', 64),
            promptTokens: 12,
            completionTokens: 7,
            estimatedCostUsd: 0.0004m,
            queryHistoryEntryId: "history-1");

        await repository.AppendAsync(audit, CancellationToken.None);
        IReadOnlyList<AiAuditEvent> recent = await repository.GetRecentAsync(10, CancellationToken.None);
        await using var stream = new MemoryStream();
        await repository.ExportToJsonAsync(stream, CancellationToken.None);
        stream.Position = 0;
        using JsonDocument doc = await JsonDocument.ParseAsync(stream);

        Assert.Single(recent);
        Assert.Equal("audit-1", recent[0].Id);
        Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal(1, await _context.AiAuditEvents.CountAsync());
    }

    [Fact]
    public async Task QueryHistoryRepository_HardDelete_LeavesAuditIntact()
    {
        var history = new AiQueryHistoryRepository(_context);
        var audit = new AiAuditRepository(_context);
        await history.AddAsync(
            new AiQueryHistoryEntry(
                "history-1",
                DateTimeOffset.UtcNow,
                "recommendation",
                "recommend",
                "summary"),
            CancellationToken.None);
        await audit.AppendAsync(
            new AiAuditEvent(
                "audit-history-1",
                DateTimeOffset.UtcNow,
                AiPrivacyTier.MetadataOnly,
                "openai",
                "gpt-test",
                new string('c', 64),
                new string('d', 64),
                queryHistoryEntryId: "history-1"),
            CancellationToken.None);

        bool softDeleted = await history.SoftDeleteAsync("history-1", CancellationToken.None);
        IReadOnlyList<AiQueryHistoryEntry> visibleAfterSoftDelete = await history.ListAsync(0, 10, CancellationToken.None);
        int hardDeleted = await history.HardDeleteAllAsync(CancellationToken.None);

        Assert.True(softDeleted);
        Assert.Empty(visibleAfterSoftDelete);
        Assert.Equal(1, hardDeleted);
        Assert.Equal(1, await _context.AiAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Phase12Migration_BackfillsLegacyHistoryIdsBeforeUniqueIndex()
    {
        (CatalogueDbContext context, string dbPath) = CatalogueTestHelper.CreateTempFileContext();
        try
        {
            IMigrator migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(Phase11Migration);
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO AiQueryHistory
                    (QueryText, ProviderKey, ModelId, PrivacyTier, RequestPayloadHash, ResponseSummary, TokensIn, TokensOut, CostEstimate, CreatedUtc, IsDeleted)
                VALUES
                    ('first query', 'local', 'model-a', 0, 'hash-a', 'first summary', 1, 2, 0.0, '2026-01-01T00:00:00+00:00', 0)
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO AiQueryHistory
                    (QueryText, ProviderKey, ModelId, PrivacyTier, RequestPayloadHash, ResponseSummary, TokensIn, TokensOut, CostEstimate, CreatedUtc, IsDeleted)
                VALUES
                    ('second query', 'local', 'model-a', 0, 'hash-b', 'second summary', 1, 2, 0.0, '2026-01-02T00:00:00+00:00', 0)
                """);

            await migrator.MigrateAsync();
            var repository = new AiQueryHistoryRepository(context);
            IReadOnlyList<AiQueryHistoryEntry> history = await repository.ListAsync(0, 10, CancellationToken.None);

            Assert.Equal(2, history.Count);
            Assert.Contains(history, entry => entry.Id == "legacy-1" && entry.QueryType == "legacy");
            Assert.Contains(history, entry => entry.Id == "legacy-2" && entry.QueryType == "legacy");

            await migrator.MigrateAsync(Phase11Migration);
            HashSet<string> phase11Columns = await GetTableColumnsAsync(context, "AiQueryHistory");
            Assert.DoesNotContain("HistoryId", phase11Columns);
            Assert.DoesNotContain("QueryType", phase11Columns);
        }
        finally
        {
            context.Dispose();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(CatalogueDbContext context, string table)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}')";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }
}
