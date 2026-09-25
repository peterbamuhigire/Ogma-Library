using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 05: the Library folders panel (D-03) and truthful terminal scan
/// states (T05.9) in the real shell view.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(1920, 1080)]
    public async Task LibraryFoldersPanel_ShowsFoldersAndNeedsAttentionWithAccessibleNames(int width, int height)
    {
        var localization = new InMemoryLocalizationService();
        var roots = new FakeRoots("Science", "History");
        var monitor = new FakeMonitor();
        var attention = new FakeAttention(3);
        MainShellViewModel shell = CreateFoldersShell(localization, roots, monitor, attention);
        await shell.LibraryFolders!.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        (Window window, CatalogueShellView view) = ShowShell(shell, width, height);

        string[] names = [.. view.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible)
            .Select(control => AutomationProperties.GetName(control))
            .OfType<string>()
            .Where(name => name.Length > 0)];
        Assert.Contains("Library folder Science, Available", names);
        Assert.Contains("Library folder History, Available", names);
        Assert.Contains("Add another library folder", names);
        Assert.Contains("Rescan library", names);
        Assert.Contains("Needs attention (3)", names);
        Assert.Contains("Remove folder Science from the library", names);
        Assert.DoesNotContain(names, name => name.Contains("OgmaLibrary.", StringComparison.Ordinal));

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task LibraryFolders_ChoosingASecondFolder_AddsItAndNeverRelinks()
    {
        var localization = new InMemoryLocalizationService();
        var roots = new FakeRoots("Science");
        var monitor = new FakeMonitor();
        MainShellViewModel shell = CreateFoldersShell(localization, roots, monitor, new FakeAttention(0));

        await shell.AddLibraryFolderAsync(Path.Combine(Path.GetTempPath(), "History"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, roots.AddCalls);
        Assert.Equal(0, roots.RelinkCalls);
        Assert.Equal(2, shell.LibraryFolders!.Folders.Count);
        Assert.Contains(monitor.Requests, request => request?.Count == 1);
        shell.Dispose();
    }

    [AvaloniaTheory]
    [InlineData(ScanOutcome.Cancelled, "Scan.Status.Cancelled")]
    [InlineData(ScanOutcome.Failed, "Scan.Status.Failed")]
    public void ScanState_TerminalOutcome_LeavesScanningAndIsShownTruthfully(ScanOutcome outcome, string key)
    {
        var localization = new InMemoryLocalizationService();
        var progress = new OgmaLibrary.Infrastructure.Ingestion.ScanProgressService();
        var monitor = new FakeMonitor();
        MainShellViewModel shell = CreateFoldersShell(
            localization, new FakeRoots(), monitor, new FakeAttention(0), progress);

        progress.SetPhase(ScanPhase.Processing);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsScanning);

        monitor.Complete(ScanSummary.Empty(outcome));
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.IsScanning);
        Assert.Equal(localization[key], shell.StatusText);
        shell.Dispose();
    }

    [AvaloniaFact]
    public void ScanState_CompletedWithIssues_ShowsTheSummary()
    {
        var localization = new InMemoryLocalizationService();
        var monitor = new FakeMonitor();
        MainShellViewModel shell = CreateFoldersShell(localization, new FakeRoots(), monitor, new FakeAttention(0));

        monitor.Complete(new ScanSummary(ScanOutcome.CompletedWithIssues, 12, 0, 4, 0, 0, 0, 1, 0));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Added 12 · Updated 0 · Needs attention 4 · Missing 0", shell.StatusText);
        Assert.Equal(shell.StatusText, shell.LibraryFolders!.LastSummaryText);
        shell.Dispose();
    }

    private static MainShellViewModel CreateFoldersShell(
        InMemoryLocalizationService localization,
        FakeRoots roots,
        FakeMonitor monitor,
        FakeAttention attention,
        IScanProgressService? progress = null)
    {
        var readModel = new EmptyCatalogueReadModel();
        var filter = new CatalogueFilterViewModel();
        var folders = new LibraryFoldersViewModel(roots, monitor, attention, localization);
        return new MainShellViewModel(
            localization,
            new CatalogueViewModel(readModel, new NullNavigation(), localization),
            new BookDetailViewModel(readModel, new NullNavigation(), localization),
            new ShelfSidebarViewModel(readModel, new NoOpCatalogueWriteService(), localization, filter),
            scanProgress: progress,
            libraryRootService: roots,
            libraryFolders: folders);
    }

    private sealed class FakeRoots : ILibraryRootService
    {
        private readonly List<LibraryRootDescriptor> _roots = [];

        public FakeRoots(params string[] names)
        {
            foreach (string name in names)
            {
                _roots.Add(Create(Path.Combine(Path.GetTempPath(), name)));
            }
        }

        public int AddCalls { get; private set; }

        public int RelinkCalls { get; private set; }

        public Task<IReadOnlyList<LibraryRootDescriptor>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRootDescriptor>>([.. _roots]);

        public Task<LibraryRootDescriptor> AddAsync(string path, string? displayName = null, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            LibraryRootDescriptor root = Create(path);
            _roots.Add(root);
            return Task.FromResult(root);
        }

        public Task<LibraryRootDescriptor> EnsureForLegacyPathAsync(string path, CancellationToken cancellationToken = default) =>
            AddAsync(path, null, cancellationToken);

        public Task<LibraryRootDescriptor> RelinkAsync(LibraryRootId rootId, string path, CancellationToken cancellationToken = default)
        {
            RelinkCalls++;
            return Task.FromResult(_roots[0]);
        }

        public Task<LibraryRootDescriptor> SetEnabledAsync(LibraryRootId rootId, bool isEnabled, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roots[0]);

        public Task RemoveAsync(LibraryRootId rootId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<LibraryRootDescriptor> RefreshHealthAsync(LibraryRootId rootId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roots[0]);

        public Task RecordSuccessfulScanAsync(LibraryRootId rootId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static LibraryRootDescriptor Create(string path) => new(
            new LibraryRootId(Ulid()),
            Path.GetFileName(path),
            path,
            null,
            LibraryRootStatus.Available,
            LibraryRootPermissionStatus.Granted,
            IsEnabled: true,
            AllowSymlinkTraversal: false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        private static string Ulid() => ("01" + Guid.NewGuid().ToString("N").ToUpperInvariant())[..26];
    }

    private sealed class FakeMonitor : ILibraryMonitor
    {
        public event EventHandler<ScanSummary>? ScanCompleted;

        public bool IsScanning => false;

        public ScanSummary? LastSummary { get; private set; }

        public List<IReadOnlyCollection<LibraryRootId>?> Requests { get; } = [];

        public Task<ScanSummary> RescanAsync(IReadOnlyCollection<LibraryRootId>? roots = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(roots);
            return Task.FromResult(ScanSummary.Empty(ScanOutcome.Completed));
        }

        public void CancelScan()
        {
        }

        public Task StartAsync(TimeSpan startupDelay, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshWatchersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Complete(ScanSummary summary)
        {
            LastSummary = summary;
            ScanCompleted?.Invoke(this, summary);
        }
    }

    private sealed class FakeAttention(int count) : ILibraryAttentionService
    {
        public Task<IReadOnlyList<NeedsAttentionItem>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NeedsAttentionItem>>([.. Enumerable.Range(0, count).Select(index =>
                new NeedsAttentionItem(
                    index,
                    null,
                    "Science",
                    $"file-{index}.pdf",
                    $"Edge/file-{index}.pdf",
                    (FileValidity)(1 + (index % 3)),
                    DateTimeOffset.UtcNow))]);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(count);

        public Task<bool> RetryAsync(long issueId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task IgnoreAsync(long issueId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetAbsolutePathAsync(long issueId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
