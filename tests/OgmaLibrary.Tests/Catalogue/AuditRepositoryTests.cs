using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Integration tests for <see cref="AuditRepository"/> verifying append-only behaviour
/// (NFR-PROD-013, Phase 04 deliverable 10).
/// </summary>
public sealed class AuditRepositoryTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly AuditRepository _repository;

    public AuditRepositoryTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        _repository = new AuditRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task AuditRepository_IsAppendOnly()
    {
        var event1 = new AuditEvent
        {
            Id = "evt-001",
            EventType = "BookAdded",
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
        };
        var event2 = new AuditEvent
        {
            Id = "evt-002",
            EventType = "ShelfCreated",
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        var event3 = new AuditEvent
        {
            Id = "evt-003",
            EventType = "AnnotationSaved",
            TimestampUtc = DateTimeOffset.UtcNow,
        };

        await _repository.AppendAsync(event1, CancellationToken.None);
        await _repository.AppendAsync(event2, CancellationToken.None);
        await _repository.AppendAsync(event3, CancellationToken.None);

        // Total count should be 3 — no deduplication or update.
        Assert.Equal(3, _context.AuditEvents.Count());

        // ReadRecentAsync returns most-recent-first.
        IReadOnlyList<AuditEvent> recent = await _repository.ReadRecentAsync(2, CancellationToken.None);
        Assert.Equal(2, recent.Count);
        Assert.Equal("AnnotationSaved", recent[0].EventType);
        Assert.Equal("ShelfCreated", recent[1].EventType);
    }

    [Fact]
    public async Task AuditRepository_AppendAsync_StoresEventType()
    {
        var auditEvent = new AuditEvent
        {
            Id = "test-evt",
            EventType = "MigrationApplied",
            EntityId = "catalogue.db",
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = "{\"migrationId\":\"InitialCatalogue\"}",
        };

        await _repository.AppendAsync(auditEvent, CancellationToken.None);

        IReadOnlyList<AuditEvent> events = await _repository.ReadRecentAsync(1, CancellationToken.None);
        Assert.Single(events);
        Assert.Equal("MigrationApplied", events[0].EventType);
    }
}
