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
/// Proves the application skeleton renders, captures a screenshot to the artifacts
/// folder for visual inspection, and runs the pseudolocale / culture-switch checks
/// (Phase 02 i18n verification).
/// </summary>
public sealed class SkeletonRenderTests
{
    private static string ArtifactsDir
    {
        get
        {
            // tests/OgmaLibrary.Tests.Ui/bin/Debug/net10.0 -> repo root.
            string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "screenshots");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }
    }

    /// <summary>Creates a MainWindowViewModel with stub/no-op services for UI tests.</summary>
    private static MainWindowViewModel CreateViewModel(InMemoryLocalizationService localization) =>
        new(localization, new NullLibrarySettingsService(), new NullIngestionOrchestrator(), new NullScanProgressService());

    [AvaloniaFact]
    public void MainWindow_RendersAndCapturesScreenshot_English()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        var window = new MainWindow { DataContext = CreateViewModel(localization) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(ArtifactsDir, "skeleton-en.png"));
    }

    [AvaloniaFact]
    public void MainWindow_RendersAndCapturesScreenshot_French()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("fr");

        var window = new MainWindow { DataContext = CreateViewModel(localization) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(ArtifactsDir, "skeleton-fr.png"));
    }

    [AvaloniaFact]
    public void MainWindow_CultureSwitch_UpdatesTitle_WithoutMissingResources()
    {
        var localization = new InMemoryLocalizationService();
        var viewModel = CreateViewModel(localization);

        localization.SetCulture("en");
        Assert.Equal("Ogma Library", viewModel.Title);

        localization.SetCulture("fr");
        Assert.Equal("Bibliothèque Ogma", viewModel.Title);

        // No key resolves to the missing-key sentinel for the skeleton window.
        Assert.DoesNotContain("⟦", viewModel.Tagline, StringComparison.Ordinal);

        viewModel.Dispose();
    }

    // ── Null/stub implementations for UI test isolation ──────────────────────────

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

    private sealed class NullIngestionOrchestrator : IIngestionOrchestrator
    {
        public Task ScanAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullScanProgressService : IScanProgressService
    {
        public ScanProgressSnapshot CurrentSnapshot { get; } =
            new(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);

        public event EventHandler<ScanProgressSnapshot>? ProgressChanged;

        public void SetPhase(ScanPhase phase) { }
        public void IncrementDiscovered() { }
        public void IncrementCompleted() { }
        public void IncrementFailed() { }
        public void Reset() { }

        // Suppress unused warning.
        private void RaiseProgressChanged() => ProgressChanged?.Invoke(this, CurrentSnapshot);
    }
}
