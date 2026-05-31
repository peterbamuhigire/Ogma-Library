using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.Views;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Headless UI test for scan progress: simulates a completed scan and asserts
/// the status text shows the scanned count. Captures a screenshot to
/// <c>artifacts/screenshots/scan-en.png</c> and copies to
/// <c>docs/developer-guide/images/scan-en.png</c>.
/// </summary>
public sealed class ScanProgressTests
{
    private static string ArtifactsDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "screenshots");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }
    }

    private static string DocsImagesDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "developer-guide", "images");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }
    }

    [AvaloniaFact]
    public void MainWindow_AfterScan_ShowsScannedCount()
    {
        // Arrange: create a fake progress service that reports 3 books scanned.
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        var fakeProgress = new FakeScanProgressService();
        var viewModel = new MainWindowViewModel(
            localization,
            new NullLibrarySettingsService(),
            new ImmediateIngestionOrchestrator(3, fakeProgress),
            fakeProgress);

        // Act: show the window and run pending dispatcher jobs.
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Simulate what happens when the orchestrator finishes (push Complete state).
        fakeProgress.SimulateCompletion(3);
        Dispatcher.UIThread.RunJobs();

        // Assert: status text shows scanned count.
        string statusText = viewModel.StatusText;
        Assert.Contains("3", statusText, StringComparison.OrdinalIgnoreCase);

        // Capture screenshot.
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        string screenshotPath = Path.Combine(ArtifactsDir, "scan-en.png");
        frame!.Save(screenshotPath);

        // Copy to docs/developer-guide/images/.
        string docsPath = Path.Combine(DocsImagesDir, "scan-en.png");
        File.Copy(screenshotPath, docsPath, overwrite: true);

        viewModel.Dispose();
    }

    // ── Test doubles ─────────────────────────────────────────────────────────────

    private sealed class FakeScanProgressService : IScanProgressService
    {
        private ScanProgressSnapshot _snapshot =
            new(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);

        public ScanProgressSnapshot CurrentSnapshot => _snapshot;

        public event EventHandler<ScanProgressSnapshot>? ProgressChanged;

        public void SetPhase(ScanPhase phase)
        {
            _snapshot = new ScanProgressSnapshot(phase,
                _snapshot.FilesDiscovered, _snapshot.FilesCompleted, _snapshot.FilesFailed, false);
            ProgressChanged?.Invoke(this, _snapshot);
        }

        public void IncrementDiscovered()
        {
            _snapshot = new ScanProgressSnapshot(_snapshot.Phase,
                _snapshot.FilesDiscovered + 1, _snapshot.FilesCompleted, _snapshot.FilesFailed, false);
        }

        public void IncrementCompleted()
        {
            _snapshot = new ScanProgressSnapshot(_snapshot.Phase,
                _snapshot.FilesDiscovered, _snapshot.FilesCompleted + 1, _snapshot.FilesFailed, false);
            ProgressChanged?.Invoke(this, _snapshot);
        }

        public void IncrementFailed()
        {
            _snapshot = new ScanProgressSnapshot(_snapshot.Phase,
                _snapshot.FilesDiscovered, _snapshot.FilesCompleted, _snapshot.FilesFailed + 1, false);
        }

        public void Reset()
        {
            _snapshot = new ScanProgressSnapshot(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);
            ProgressChanged?.Invoke(this, _snapshot);
        }

        public void SimulateCompletion(int bookCount)
        {
            _snapshot = new ScanProgressSnapshot(
                ScanPhase.Complete,
                bookCount, bookCount, 0, IsCancellable: false);
            ProgressChanged?.Invoke(this, _snapshot);
        }
    }

    private sealed class ImmediateIngestionOrchestrator : IIngestionOrchestrator
    {
        private readonly int _bookCount;
        private readonly FakeScanProgressService _progress;

        public ImmediateIngestionOrchestrator(int bookCount, FakeScanProgressService progress)
        {
            _bookCount = bookCount;
            _progress = progress;
        }

        public Task ScanAsync(CancellationToken cancellationToken = default)
        {
            _progress.SimulateCompletion(_bookCount);
            return Task.CompletedTask;
        }
    }

    private sealed class NullLibrarySettingsService : ILibrarySettingsService
    {
        public Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task SetExcludedFoldersAsync(IReadOnlyList<string> excludedFolders, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
