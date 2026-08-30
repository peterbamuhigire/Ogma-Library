using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Startup;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Rendered evidence for Phase 02 startup, failure and recovery states.</summary>
public sealed class StartupShellRenderTests
{
    [AvaloniaFact]
    public void BootstrapShell_RendersBeforeApplicationComposition()
    {
        var localization = new InMemoryLocalizationService();
        StartupShellViewModel viewModel = StartupShellViewModel.CreateBootstrap(localization);
        var window = new DesktopShellWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsStarting);
        Assert.True(viewModel.IsLoadingVisible);
        Assert.False(viewModel.IsLibraryVisible);
        Assert.False(Assert.IsType<Grid>(window.FindControl<Grid>("LibrarySurface")).IsVisible);
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(SkeletonRenderTests.ArtifactsDir, "startup-bootstrap-en.png"));
        window.Close();
    }

    [AvaloniaFact]
    public async Task RequiredFailure_RendersFocusedRecoverableDegradedShell()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel mainShell = SkeletonRenderTests.CreateViewModel(localization);
        var coordinator = new FixedStartupCoordinator(requiredFailure: true);
        var viewModel = new StartupShellViewModel(
            coordinator,
            mainShell,
            new OgmaRuntimeOptions
            {
                DataDirectory = Path.GetTempPath(),
                LibraryRoot = Path.GetTempPath(),
            },
            localization);
        var window = new DesktopShellWindow { DataContext = viewModel };
        window.Show();

        await viewModel.StartAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.False(viewModel.IsLibraryVisible);
        Assert.True(viewModel.IsDegraded);
        Assert.True(viewModel.CanRetry);
        Assert.False(Assert.IsType<Grid>(window.FindControl<Grid>("LibrarySurface")).IsVisible);
        Assert.Equal(2, viewModel.Issues.Count);
        Button retry = Assert.IsType<Button>(window.FindControl<Button>("RetryButton"));
        Assert.True(retry.IsEnabled);
        Assert.True(retry.IsFocused);
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(SkeletonRenderTests.ArtifactsDir, "startup-degraded-en.png"));

        window.Close();
        mainShell.Dispose();
    }

    [AvaloniaFact]
    public async Task OptionalFailure_KeepsCatalogueVisibleBesideRecoveryPanel()
    {
        var localization = new InMemoryLocalizationService();
        MainShellViewModel mainShell = SkeletonRenderTests.CreateViewModel(localization);
        var viewModel = new StartupShellViewModel(
            new FixedStartupCoordinator(requiredFailure: false),
            mainShell,
            new OgmaRuntimeOptions
            {
                DataDirectory = Path.GetTempPath(),
                LibraryRoot = Path.GetTempPath(),
            },
            localization);
        var window = new DesktopShellWindow { DataContext = viewModel };
        window.Show();

        await viewModel.StartAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsLibraryVisible);
        Assert.True(viewModel.IsDegraded);
        Assert.True(Assert.IsType<Grid>(window.FindControl<Grid>("LibrarySurface")).IsVisible);
        Assert.Single(viewModel.Issues);
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(SkeletonRenderTests.ArtifactsDir, "startup-partial-en.png"));

        window.Close();
        mainShell.Dispose();
    }

    private sealed class FixedStartupCoordinator : IApplicationStartupCoordinator
    {
        private readonly bool _requiredFailure;

        public FixedStartupCoordinator(bool requiredFailure) =>
            _requiredFailure = requiredFailure;

        public Task<ApplicationStartupReport> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return Task.FromResult(new ApplicationStartupReport(
                now,
                now,
                CanOpenCatalogue: !_requiredFailure,
                Tasks: _requiredFailure
                    ?
                    [
                        new StartupTaskReport(
                            "catalogue.migration",
                            StartupTaskCriticality.Required,
                            Succeeded: false,
                            "io_unavailable",
                            "The catalogue could not be prepared. Your PDF files were not changed.",
                            TimeSpan.FromMilliseconds(8)),
                    ]
                    :
                    [
                        new StartupTaskReport(
                            "catalogue.migration",
                            StartupTaskCriticality.Required,
                            Succeeded: true,
                            "ready",
                            "Ready",
                            TimeSpan.FromMilliseconds(8)),
                        new StartupTaskReport(
                            "workers.start",
                            StartupTaskCriticality.Optional,
                            Succeeded: false,
                            "timeout",
                            "Background processing is paused and can be retried.",
                            TimeSpan.FromMilliseconds(12)),
                    ],
                Capabilities: _requiredFailure
                    ?
                    [
                        new CapabilityHealth(
                            "pdf.worker",
                            CapabilityAvailability.Unavailable,
                            "worker_not_found",
                            "PDF processing is unavailable. Catalogue recovery remains safe."),
                    ]
                    : []));
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
