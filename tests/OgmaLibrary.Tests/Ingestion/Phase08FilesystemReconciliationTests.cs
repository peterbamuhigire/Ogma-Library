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
            NewOccurrence(
                "01PH08OCCURRENCE0000000002",
                "missing.pdf",
                availability: 0,
                missingSinceUtc: DateTimeOffset.UtcNow.AddDays(-2)));
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

    [Fact]
    public async Task ExactHashMoveFollowsOccurrence_AndReplacementInvalidatesAssetBinding()
    {
        const string assetId = "01PH08ASSET000000000000001";
        const string hashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ScanSessionDescriptor session = await _processing.StartSessionAsync(_rootId);
        _context.ContentAssets.Add(new ContentAssetRow
        {
            ContentAssetId = assetId,
            Sha256Hash = hashA,
            FingerprintVersion = 1,
            VerificationStatus = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.FileOccurrences.AddRange(
            NewOccurrence("01PH08OCCURRENCE0000000004", "old.pdf", availability: 1, assetId),
            NewOccurrence("01PH08OCCURRENCE0000000005", "replace.pdf", availability: 0, assetId));
        _context.DiscoveryObservations.AddRange(
            NewObservation(session.Id, "new.pdf", hashA),
            NewObservation(session.Id, "replace.pdf",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        AddCompleteCheckpoint();
        await _context.SaveChangesAsync();

        ReconciliationResult result = await _reconciliation.ReconcileAsync(session.Id);

        Assert.Equal(ReconciliationOutcome.Applied, result.Outcome);
        Assert.Equal(1, result.MovedOccurrences);
        Assert.Equal(1, result.ReplacementOccurrences);
        Assert.Equal(1, result.InvalidatedStageExecutions);
        FileOccurrenceRow moved = await _context.FileOccurrences
            .SingleAsync(row => row.FileOccurrenceId == "01PH08OCCURRENCE0000000004");
        FileOccurrenceRow replaced = await _context.FileOccurrences
            .SingleAsync(row => row.FileOccurrenceId == "01PH08OCCURRENCE0000000005");
        Assert.Equal("new.pdf", moved.NormalizedRelativePath);
        Assert.Equal(0, moved.AvailabilityStatus);
        Assert.Null(replaced.ContentAssetId);
        Assert.Equal(
            (int)StageExecutionStatus.Pending,
            await _context.StageExecutions.Select(stage => stage.Status).SingleAsync());
    }

    [Fact]
    public async Task MissingOccurrence_IsDeferredDuringGraceAndMarkedUnavailableAfterward()
    {
        ScanSessionDescriptor session = await _processing.StartSessionAsync(_rootId);
        _context.FileOccurrences.Add(NewOccurrence(
            "01PH08OCCURRENCE0000000006",
            "grace.pdf",
            availability: 0));
        AddCompleteCheckpoint();
        await _context.SaveChangesAsync();

        ReconciliationResult first = await _reconciliation.ReconcileAsync(session.Id);

        Assert.Equal(1, first.DeferredMissingOccurrences);
        FileOccurrenceRow occurrence = await _context.FileOccurrences.SingleAsync();
        Assert.Equal(0, occurrence.AvailabilityStatus);
        Assert.NotNull(occurrence.MissingSinceUtc);

        occurrence.MissingSinceUtc = DateTimeOffset.UtcNow.AddDays(-2);
        await _context.SaveChangesAsync();
        ReconciliationResult second = await _reconciliation.ReconcileAsync(session.Id);

        Assert.Equal(1, second.MarkedUnavailableOccurrences);
        Assert.Equal(1, (await _context.FileOccurrences.SingleAsync()).AvailabilityStatus);
    }

    [Fact]
    public async Task AmbiguousExactHash_IsHeldForReviewWithoutGuessing()
    {
        const string assetId = "01PH08ASSET000000000000002";
        const string hash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        ScanSessionDescriptor session = await _processing.StartSessionAsync(_rootId);
        _context.ContentAssets.Add(new ContentAssetRow
        {
            ContentAssetId = assetId,
            Sha256Hash = hash,
            FingerprintVersion = 1,
            VerificationStatus = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.FileOccurrences.Add(NewOccurrence(
            "01PH08OCCURRENCE0000000007",
            "old.pdf",
            availability: 0,
            assetId));
        _context.DiscoveryObservations.AddRange(
            NewObservation(session.Id, "candidate-a.pdf", hash),
            NewObservation(session.Id, "candidate-b.pdf", hash));
        AddCompleteCheckpoint();
        await _context.SaveChangesAsync();

        ReconciliationResult result = await _reconciliation.ReconcileAsync(session.Id);

        Assert.Equal(1, result.AmbiguousOccurrences);
        Assert.Equal(0, result.MovedOccurrences);
        Assert.Equal(0, (await _context.FileOccurrences.SingleAsync()).AvailabilityStatus);
        Assert.Contains(
            result.AuditSummary ?? [],
            summary => summary.ReasonCode == "ambiguous_relocation_review" && summary.Count == 1);
        ReconciliationReviewRow review = await _context.ReconciliationReviews.SingleAsync();
        Assert.Equal("ambiguous_relocation_review", review.ReasonCode);
        Assert.Contains("candidate-a.pdf", review.CandidatePathsJson);
        Assert.Contains("candidate-b.pdf", review.CandidatePathsJson);
    }

    [Fact]
    public async Task RelocationReview_AcceptRequiresPersistedCandidateAndRestoresOccurrence()
    {
        _context.ReconciliationReviews.Add(new ReconciliationReviewRow
        {
            LibraryRootId = _rootId.Value,
            FileOccurrenceId = "01PH08OCCURRENCE0000000008",
            ReasonCode = "ambiguous_relocation_review",
            CandidatePathsJson = "[\"candidate-a.pdf\",\"candidate-b.pdf\"]",
            Status = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.FileOccurrences.Add(NewOccurrence(
            "01PH08OCCURRENCE0000000008", "old.pdf", availability: 1));
        await _context.SaveChangesAsync();

        var service = new ReconciliationReviewService(_context);
        IReadOnlyList<ReconciliationReviewDescriptor> pending =
            await service.ListPendingAsync(_rootId.Value);

        ReconciliationReviewDescriptor review = Assert.Single(pending);
        Assert.Equal(["candidate-a.pdf", "candidate-b.pdf"], review.CandidatePaths);

        await service.DecideAsync(
            review.ReviewId,
            ReconciliationReviewDecision.Accept,
            "candidate-b.pdf");

        FileOccurrenceRow occurrence = await _context.FileOccurrences.SingleAsync();
        ReconciliationReviewRow persisted = await _context.ReconciliationReviews.SingleAsync();
        Assert.Equal("candidate-b.pdf", occurrence.NormalizedRelativePath);
        Assert.Equal(0, occurrence.AvailabilityStatus);
        Assert.Null(occurrence.MissingSinceUtc);
        Assert.Equal(1, persisted.Status);
        Assert.NotNull(persisted.DecidedUtc);
        Assert.Contains(
            await _context.AuditEvents.ToListAsync(),
            row => row.AfterJson?.Contains("relocation_review_accepted", StringComparison.Ordinal) == true);
        Assert.Empty(await service.ListPendingAsync(_rootId.Value));
    }

    [Fact]
    public async Task RelocationReview_RejectKeepsOccurrenceAndRejectsArbitraryPath()
    {
        _context.ReconciliationReviews.Add(new ReconciliationReviewRow
        {
            LibraryRootId = _rootId.Value,
            FileOccurrenceId = "01PH08OCCURRENCE0000000009",
            ReasonCode = "ambiguous_relocation_review",
            CandidatePathsJson = "[\"candidate.pdf\"]",
            Status = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.FileOccurrences.Add(NewOccurrence(
            "01PH08OCCURRENCE0000000009", "old.pdf", availability: 1));
        await _context.SaveChangesAsync();

        var service = new ReconciliationReviewService(_context);
        long reviewId = (await service.ListPendingAsync()).Single().ReviewId;

        await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(
            reviewId,
            ReconciliationReviewDecision.Accept,
            "../escape.pdf"));
        Assert.Equal("old.pdf", (await _context.FileOccurrences.SingleAsync()).NormalizedRelativePath);

        await service.DecideAsync(reviewId, ReconciliationReviewDecision.Reject);

        FileOccurrenceRow occurrence = await _context.FileOccurrences.SingleAsync();
        ReconciliationReviewRow persisted = await _context.ReconciliationReviews.SingleAsync();
        Assert.Equal("old.pdf", occurrence.NormalizedRelativePath);
        Assert.Equal(1, occurrence.AvailabilityStatus);
        Assert.Equal(2, persisted.Status);
        Assert.Contains(
            await _context.AuditEvents.ToListAsync(),
            row => row.AfterJson?.Contains("relocation_review_rejected", StringComparison.Ordinal) == true);
    }

    private FileOccurrenceRow NewOccurrence(
        string id,
        string path,
        int availability,
        string? assetId = null,
        DateTimeOffset? missingSinceUtc = null) => new()
        {
            FileOccurrenceId = id,
            LibraryRootId = _rootId.Value,
            RelativePath = path,
            NormalizedRelativePath = path,
            AvailabilityStatus = availability,
            ContentAssetId = assetId,
            MissingSinceUtc = missingSinceUtc,
        };

    private DiscoveryObservationRow NewObservation(long sessionId, string path, string hash) => new()
    {
        LibraryRootId = _rootId.Value,
        NormalizedRelativePath = path,
        SizeBytes = 10,
        ModifiedUtcTicks = 1,
        Sha256Hash = hash,
        LastObservedScanSessionId = sessionId,
        FirstSeenUtc = DateTimeOffset.UtcNow,
        LastSeenUtc = DateTimeOffset.UtcNow,
    };

    private void AddCompleteCheckpoint()
    {
        _context.DirectoryCheckpoints.Add(new DirectoryCheckpointRow
        {
            LibraryRootId = _rootId.Value,
            NormalizedRelativeDirectory = string.Empty,
            LastCompletedUtc = DateTimeOffset.UtcNow,
            LastObservedFileCount = 2,
        });
    }
}
