using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Phase 8 acceptance tests for evidence-gated availability recovery.</summary>
public sealed class Phase08FilesystemReconciliationTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly LibraryRootId _rootId = new("01PH08ROOT0000000000000001");
    private readonly ProcessingStateService _processing;
    private readonly FilesystemReconciliationService _reconciliation;

    public Phase08FilesystemReconciliationTests()
    {
        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = _rootId.Value,
            DisplayName = "Phase 8 root",
            CanonicalLocator = Path.GetTempPath(),
            RootStatus = (int)LibraryRootStatus.Available,
            PermissionStatus = (int)LibraryRootPermissionStatus.Granted,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.SaveChanges();
        _processing = new ProcessingStateService(_context);
        _reconciliation = new FilesystemReconciliationService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task HealthyCompleteSession_MarksAbsentOccurrenceUnavailableAndRestoresObserved()
    {
        ScanSessionDescriptor session = await _processing.StartSessionAsync(_rootId);
        _context.FileOccurrences.AddRange(
            NewOccurrence("01PH08OCCURRENCE0000000001", "present.pdf", availability: 1),
            NewOccurrence("01PH08OCCURRENCE0000000002", "missing.pdf", availability: 0));
        _context.DiscoveryObservations.Add(new DiscoveryObservationRow
        {
            LibraryRootId = _rootId.Value,
            NormalizedRelativePath = "present.pdf",
            SizeBytes = 10,
            ModifiedUtcTicks = 1,
            LastObservedScanSessionId = session.Id,
            FirstSeenUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow,
        });
        _context.DirectoryCheckpoints.Add(new DirectoryCheckpointRow
        {
            LibraryRootId = _rootId.Value,
            NormalizedRelativeDirectory = string.Empty,
            LastCompletedUtc = DateTimeOffset.UtcNow,
            LastObservedFileCount = 1,
        });
        await _context.SaveChangesAsync();

        ReconciliationResult result = await _reconciliation.ReconcileAsync(session.Id);

        Assert.Equal(ReconciliationOutcome.Applied, result.Outcome);
        Assert.Equal(1, result.RestoredOccurrences);
        Assert.Equal(1, result.MarkedUnavailableOccurrences);
        FileOccurrenceRow present = await _context.FileOccurrences
            .SingleAsync(row => row.FileOccurrenceId == "01PH08OCCURRENCE0000000001");
        FileOccurrenceRow missing = await _context.FileOccurrences
            .SingleAsync(row => row.FileOccurrenceId == "01PH08OCCURRENCE0000000002");
        Assert.Equal(0, present.AvailabilityStatus);
        Assert.Equal(1, missing.AvailabilityStatus);
        Assert.Equal(2, await _context.AuditEvents
            .CountAsync(eventRow => eventRow.EventType == "FilesystemReconciliation"));
    }

    [Fact]
    public async Task RootOutageOrIncompleteScan_DoesNotMutateOccurrences()
    {
        ScanSessionDescriptor session = await _processing.StartSessionAsync(_rootId);
        FileOccurrenceRow occurrence = NewOccurrence(
            "01PH08OCCURRENCE0000000003", "outage.pdf", availability: 0);
        _context.FileOccurrences.Add(occurrence);
        await _context.SaveChangesAsync();

        _context.LibraryRoots.Single().RootStatus = (int)LibraryRootStatus.Unavailable;
        await _context.SaveChangesAsync();
        ReconciliationResult outage = await _reconciliation.ReconcileAsync(session.Id);
        Assert.Equal(ReconciliationOutcome.RootUnavailable, outage.Outcome);
        Assert.Equal(0, (await _context.FileOccurrences.SingleAsync()).AvailabilityStatus);

        _context.LibraryRoots.Single().RootStatus = (int)LibraryRootStatus.Available;
        await _context.SaveChangesAsync();
        ReconciliationResult incomplete = await _reconciliation.ReconcileAsync(session.Id);
        Assert.Equal(ReconciliationOutcome.IncompleteScan, incomplete.Outcome);
        Assert.Equal(0, (await _context.FileOccurrences.SingleAsync()).AvailabilityStatus);
        Assert.Empty(await _context.AuditEvents.ToListAsync());
    }

    private FileOccurrenceRow NewOccurrence(string id, string path, int availability) => new()
    {
        FileOccurrenceId = id,
        LibraryRootId = _rootId.Value,
        RelativePath = path,
        NormalizedRelativePath = path,
        AvailabilityStatus = availability,
    };
}
