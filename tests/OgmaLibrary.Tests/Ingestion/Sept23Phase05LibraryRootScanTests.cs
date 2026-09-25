using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Tests.Catalogue;
using PdfSharp.Pdf;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Sept-23 Phase 05 acceptance tests: multiple library roots (D-03), per-root file
/// identity (T05.2/T05.3), validity and Needs attention (T05.6), missing-status
/// restore (T05.7) and terminal scan states (T05.9).
/// </summary>
public sealed class Sept23Phase05LibraryRootScanTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p05-{Guid.NewGuid():N}");
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly LibrarySettingsService _settings;
    private readonly ScanProgressService _progress = new();
    private readonly LibraryRootService _roots;

    public Sept23Phase05LibraryRootScanTests()
    {
        Directory.CreateDirectory(_temp);
        _settings = new LibrarySettingsService(Path.Combine(_temp, "data"));
        _roots = new LibraryRootService(_context, new FileSystemLibraryRootPlatformAdapter());
    }

    public void Dispose()
    {
        _settings.Dispose();
        _context.Dispose();
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary library.
        }
    }

    [Fact]
    public async Task LibraryRoot_SecondFolder_NeverMarksFirstFolderMissing()
    {
        string science = Library("Science", ("light.pdf", "Light"), ("ecology.pdf", "Ecology"));
        string history = Library("History", ("kingdoms.pdf", "Kingdoms"));

        await _roots.AddAsync(science);
        await Orchestrator().ScanAsync();
        await _roots.AddAsync(history);
        ScanSummary summary = await Orchestrator().ScanRootsAsync(null);

        Assert.Equal(ScanOutcome.Completed, summary.Outcome);
        Assert.Equal(0, summary.Missing);
        Assert.Equal(3, await _context.Books.CountAsync());
        Assert.All(await _context.BookFiles.ToListAsync(), file => Assert.Equal(0, file.FileStatus));
        Assert.Equal(3, (await Summaries()).Count);
    }

    [Fact]
    public async Task LibraryRoot_SameRelativePathInTwoRoots_ResolvesEachThroughItsOwnRoot()
    {
        string a = Library("A", ("shared/same.pdf", "Book A"));
        string b = Library("B", ("shared/same.pdf", "Book B"));
        await _roots.AddAsync(a);
        await _roots.AddAsync(b);

        await Orchestrator().ScanAsync();

        List<BookFileRow> files = await _context.BookFiles.AsNoTracking().ToListAsync();
        Assert.Equal(2, files.Count);
        Assert.Equal(2, files.Select(file => file.LibraryRootId).Distinct().Count());
        var locator = new BookFileLocator(_context, _settings);
        var located = new List<string?>();
        foreach (BookFileRow file in files)
        {
            located.Add(await locator.LocateAsync(file.BookId, CancellationToken.None));
        }

        Assert.Contains(Path.Combine(a, "shared", "same.pdf"), located);
        Assert.Contains(Path.Combine(b, "shared", "same.pdf"), located);
    }

    [Fact]
    public async Task LibraryRoot_RemoveHidesOnlyItsBooks_AndReAddRestoresThem()
    {
        string science = Library("Science", ("light.pdf", "Light"));
        string history = Library("History", ("kingdoms.pdf", "Kingdoms"), ("routes.pdf", "Routes"));
        LibraryRootDescriptor scienceRoot = await _roots.AddAsync(science);
        await _roots.AddAsync(history);
        await Orchestrator().ScanAsync();
        Assert.Equal(3, (await Summaries()).Count);

        await _roots.RemoveAsync(scienceRoot.Id);
        Assert.Equal(2, (await Summaries()).Count);
        Assert.Single(await _roots.ListAsync());
        Assert.Equal(3, await _context.Books.CountAsync()); // nothing deleted

        LibraryRootDescriptor restored = await _roots.AddAsync(science);
        Assert.Equal(scienceRoot.Id, restored.Id);
        Assert.Equal(3, (await Summaries()).Count);
    }

    [Fact]
    public async Task LibraryRoot_NestedOrParentFolder_IsRejected()
    {
        string lib = Library("Lib", ("Science/one.pdf", "One"));
        await _roots.AddAsync(lib);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _roots.AddAsync(Path.Combine(lib, "Science")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _roots.AddAsync(_temp));
        Assert.Single(await _roots.ListAsync());
    }

    [Fact]
    public async Task LibraryRoot_DisabledRootIsHiddenAndNotScanned()
    {
        string science = Library("Science", ("light.pdf", "Light"));
        LibraryRootDescriptor root = await _roots.AddAsync(science);
        await Orchestrator().ScanAsync();

        await _roots.SetEnabledAsync(root.Id, false);
        Assert.Empty(await Summaries());
        ScanSummary summary = await Orchestrator().ScanRootsAsync(null);
        Assert.Equal(0, summary.RootsScanned);

        await _roots.SetEnabledAsync(root.Id, true);
        Assert.Single(await Summaries());
    }

    [Fact]
    public async Task LibraryRoot_FolderRenamedAwayAndBack_BooksReturnToAvailable()
    {
        string lib = Library("Lib", ("a/one.pdf", "One"), ("a/two.pdf", "Two"));
        await _roots.AddAsync(lib);
        await Orchestrator().ScanAsync();

        // Renamed out of the library folder (a move inside the folder is a relocation).
        string away = Path.Combine(_temp, "a-away");
        Directory.Move(Path.Combine(lib, "a"), away);
        ScanSummary missing = await Orchestrator().ScanRootsAsync(null);
        Assert.Equal(2, missing.Missing);
        Assert.All(await _context.Books.AsNoTracking().ToListAsync(), book => Assert.Equal(1, book.Status));

        Directory.Move(away, Path.Combine(lib, "a"));
        ScanSummary restored = await Orchestrator().ScanRootsAsync(null);

        Assert.Equal(2, restored.Restored);
        Assert.All(await _context.Books.AsNoTracking().ToListAsync(), book => Assert.Equal(0, book.Status));
        Assert.All(await _context.BookFiles.AsNoTracking().ToListAsync(), file => Assert.Equal(0, file.FileStatus));
        Assert.All((await Summaries()), summary => Assert.True(summary.IsAvailable));
        Assert.Equal(2, await _context.AuditEvents.CountAsync(evt => evt.EventType == "BookRestored"));
    }

    [Fact]
    public async Task LibraryRoot_OfflineRoot_KeepsBooksAndReportsOffline()
    {
        string lib = Library("Removable", ("one.pdf", "One"));
        await _roots.AddAsync(lib);
        await Orchestrator().ScanAsync();

        Directory.Move(lib, lib + "-unplugged");
        ScanSummary summary = await Orchestrator().ScanRootsAsync(null);

        Assert.Equal(1, summary.RootsOffline);
        Assert.Equal(0, summary.Missing);
        Assert.Equal(ScanOutcome.CompletedWithIssues, summary.Outcome);
        Assert.Equal(0, (await _context.Books.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(LibraryRootStatus.Unavailable, (await _roots.ListAsync()).Single().Status);
    }

    [Fact]
    public async Task FileValidity_InvalidFilesGoToNeedsAttention_LockedIsABook()
    {
        string lib = Library("Edge", ("good.pdf", "Good"));
        await File.WriteAllBytesAsync(Path.Combine(lib, "empty.pdf"), []);
        await File.WriteAllTextAsync(Path.Combine(lib, "not-really-a.pdf"), "<html>this is not a pdf</html>");
        byte[] good = await File.ReadAllBytesAsync(Path.Combine(lib, "good.pdf"));
        await File.WriteAllBytesAsync(Path.Combine(lib, "Truncated Download.pdf"), good[..(good.Length / 3)]);
        using (var locked = new PdfDocument())
        {
            locked.AddPage();
            locked.SecuritySettings.UserPassword = "secret";
            locked.SecuritySettings.OwnerPassword = "owner";
            locked.Save(Path.Combine(lib, "Locked Ledger.pdf"));
        }

        await _roots.AddAsync(lib);
        ScanSummary summary = await Orchestrator().ScanRootsAsync(null);

        Assert.Equal(ScanOutcome.CompletedWithIssues, summary.Outcome);
        Assert.Equal(3, summary.NeedsAttention);
        Assert.Equal(2, summary.Added);
        Assert.Equal(2, await _context.Books.CountAsync());
        IReadOnlyList<BookSummaryProjection> books = await Summaries();
        Assert.Equal(2, books.Count);
        Assert.Single(books, book => book.IsLocked);

        var attention = new LibraryAttentionService(_context, new PdfFileValidityClassifier());
        IReadOnlyList<NeedsAttentionItem> items = await attention.ListAsync();
        Assert.Equal(
            [FileValidity.Empty, FileValidity.NotAPdf, FileValidity.Damaged],
            items.Select(item => item.Reason).Order().ToArray());
        Assert.Equal(3, await attention.CountAsync());

        // Ignore hides an item; Retry on a repaired file clears it for the next scan.
        NeedsAttentionItem empty = items.Single(item => item.Reason == FileValidity.Empty);
        await attention.IgnoreAsync(empty.IssueId);
        Assert.Equal(2, await attention.CountAsync());
        NeedsAttentionItem truncated = items.Single(item => item.Reason == FileValidity.Damaged);
        // A distinct repaired PDF: a byte-identical copy of another book would be attached to
        // that book as a second occurrence (Sept-23 Phase 06, T06.9).
        IngestionTestFixture.WriteSyntheticPdf(Path.Combine(lib, "Truncated Download.pdf"), "Repaired", "Author");
        Assert.True(await attention.RetryAsync(truncated.IssueId));
        await Orchestrator().ScanRootsAsync(null);
        Assert.Equal(1, await attention.CountAsync());
        Assert.Equal(3, (await Summaries()).Count);
    }

    [Fact]
    public async Task FileValidity_TrackedBookThatBecomesUnreadable_IsHiddenNotDeleted()
    {
        string lib = Library("Lib", ("book.pdf", "Book"));
        await _roots.AddAsync(lib);
        await Orchestrator().ScanAsync();
        Assert.Single(await Summaries());

        await File.WriteAllBytesAsync(Path.Combine(lib, "book.pdf"), []);
        await Orchestrator().ScanAsync();

        Assert.Empty(await Summaries());
        Assert.Equal(1, await _context.Books.CountAsync());
        Assert.Equal((int)FileValidity.Empty, (await _context.BookFiles.AsNoTracking().SingleAsync()).FileValidity);
    }

    [Fact]
    public async Task LibraryRoot_LegacySingleRootRows_AreBackfilledTransactionally()
    {
        string lib = Library("Legacy", ("old.pdf", "Old"));
        await _settings.SetLibraryRootAsync(lib);
        _context.Books.Add(new BookRow { BookId = "01LEGACYBOOK00000000000001", Status = 0 });
        _context.BookFiles.Add(new BookFileRow
        {
            BookId = "01LEGACYBOOK00000000000001",
            RelativePath = "old.pdf",
            FileStatus = 0,
            LastSeenUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        string? rootId = await LibraryRootPaths.BackfillLegacyRootAsync(_context, lib, CancellationToken.None);

        Assert.NotNull(rootId);
        Assert.Equal(rootId, (await _context.BookFiles.AsNoTracking().SingleAsync()).LibraryRootId);
        Assert.Equal(1, await _context.AuditEvents.CountAsync(evt => evt.EventType == "LibraryRootBackfilled"));
        Assert.Equal(
            Path.Combine(lib, "old.pdf"),
            await new BookFileLocator(_context, _settings).LocateAsync("01LEGACYBOOK00000000000001", CancellationToken.None));

        // Idempotent: a second pass assigns nothing new.
        Assert.Equal(rootId, await LibraryRootPaths.BackfillLegacyRootAsync(_context, lib, CancellationToken.None));
        Assert.Equal(1, await _context.AuditEvents.CountAsync(evt => evt.EventType == "LibraryRootBackfilled"));
    }

    [Fact]
    public async Task ScanState_CancelDuringProcessing_EndsCancelled()
    {
        string lib = Library("Lib", ("one.pdf", "One"), ("two.pdf", "Two"));
        await _roots.AddAsync(lib);
        using var cancellation = new CancellationTokenSource();
        var discovery = new CancellingDiscovery(new PdfDiscoveryService(), cancellation);

        ScanSummary summary = await Orchestrator(discovery).ScanRootsAsync(null, cancellation.Token);

        Assert.Equal(ScanOutcome.Cancelled, summary.Outcome);
        Assert.Equal(ScanPhase.Cancelled, _progress.CurrentSnapshot.Phase);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Orchestrator(new CancellingDiscovery(new PdfDiscoveryService(), new CancellationTokenSource()))
                .ScanAsync(new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task ScanState_UnexpectedFailure_EndsFailedAndRethrows()
    {
        string lib = Library("Lib", ("one.pdf", "One"));
        await _roots.AddAsync(lib);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Orchestrator(new ThrowingDiscovery()).ScanRootsAsync(null));

        Assert.Equal(ScanPhase.Failed, _progress.CurrentSnapshot.Phase);
    }

    [Fact]
    public async Task ScanState_SuccessfulScan_EndsComplete()
    {
        string lib = Library("Lib", ("one.pdf", "One"));
        await _roots.AddAsync(lib);

        ScanSummary summary = await Orchestrator().ScanRootsAsync(null);

        Assert.Equal(ScanOutcome.Completed, summary.Outcome);
        Assert.Equal(ScanPhase.Complete, _progress.CurrentSnapshot.Phase);
        Assert.Equal(1, summary.Added);
        Assert.Equal(1, summary.RootsScanned);
    }

    [Fact]
    public async Task LibraryMonitor_WatcherPicksUpNewPdfWithoutUserAction()
    {
        string lib = Library("Watched", ("first.pdf", "First"));
        await _roots.AddAsync(lib);
        using var monitor = new LibraryMonitorService(
            Orchestrator(), _context, _settings, TimeSpan.FromMilliseconds(300));
        await monitor.RunStartupAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.Equal(1, monitor.WatchedRootCount);
        Assert.Equal(1, await _context.Books.CountAsync());

        var completed = new TaskCompletionSource<ScanSummary>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.ScanCompleted += (_, summary) =>
        {
            if (summary.Added > 0)
            {
                completed.TrySetResult(summary);
            }
        };
        IngestionTestFixture.WriteSyntheticPdf(Path.Combine(lib, "second.pdf"), "Second", "Author");

        ScanSummary picked = await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        monitor.Dispose();
        Assert.Equal(1, picked.Added);
        Assert.Equal(2, await _context.Books.CountAsync());
    }

    [Fact]
    public async Task LibraryMonitor_CancelScan_ReportsCancelled()
    {
        string lib = Library("Lib", ("one.pdf", "One"));
        await _roots.AddAsync(lib);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var monitor = new LibraryMonitorService(Orchestrator(), _context, _settings, TimeSpan.FromSeconds(1));

        ScanSummary summary = await monitor.RescanAsync(null, cancellation.Token);

        Assert.Equal(ScanOutcome.Cancelled, summary.Outcome);
        Assert.Equal(summary, monitor.LastSummary);
        Assert.False(monitor.IsScanning);
    }

    private IngestionOrchestrator Orchestrator(IPdfDiscoveryService? discovery = null) =>
        new(
            _settings,
            discovery ?? new PdfDiscoveryService(),
            new BookIdentityService(_context),
            new BookRegistrationService(_context),
            new UnavailableFileFlagService(_context),
            _progress,
            _context);

    private async Task<IReadOnlyList<BookSummaryProjection>> Summaries()
    {
        var readModel = new CatalogueReadModel(_context);
        var list = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection summary in readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            list.Add(summary);
        }

        return list;
    }

    private string Library(string name, params (string RelativePath, string Title)[] books)
    {
        string root = Path.Combine(_temp, name);
        Directory.CreateDirectory(root);
        foreach ((string relativePath, string title) in books)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            IngestionTestFixture.WriteSyntheticPdf(path, title, "Author " + title);
        }

        return root;
    }

    private sealed class CancellingDiscovery(IPdfDiscoveryService inner, CancellationTokenSource cancellation)
        : IPdfDiscoveryService
    {
        public async Task DiscoverAsync(
            string rootPath,
            IReadOnlyList<string> excludedFolders,
            ChannelWriter<DiscoveredFile> writer,
            Func<DiscoveryDirectoryDiagnostic, ValueTask>? directoryDiagnosticSink = null,
            string? resumeAfterRelativeDirectory = null,
            CancellationToken cancellationToken = default)
        {
            var relay = Channel.CreateUnbounded<DiscoveredFile>();
            await inner.DiscoverAsync(rootPath, excludedFolders, relay.Writer, cancellationToken: CancellationToken.None);
            bool first = true;
            await foreach (DiscoveredFile file in relay.Reader.ReadAllAsync(CancellationToken.None))
            {
                await writer.WriteAsync(file, CancellationToken.None);
                if (first)
                {
                    // Cancel after the first file reaches registration.
                    first = false;
                    await cancellation.CancelAsync();
                }
            }

            writer.TryComplete();
        }
    }

    private sealed class ThrowingDiscovery : IPdfDiscoveryService
    {
        public Task DiscoverAsync(
            string rootPath,
            IReadOnlyList<string> excludedFolders,
            ChannelWriter<DiscoveredFile> writer,
            Func<DiscoveryDirectoryDiagnostic, ValueTask>? directoryDiagnosticSink = null,
            string? resumeAfterRelativeDirectory = null,
            CancellationToken cancellationToken = default)
        {
            writer.TryComplete(new InvalidOperationException("Simulated discovery failure."));
            return Task.FromException(new InvalidOperationException("Simulated discovery failure."));
        }
    }
}
