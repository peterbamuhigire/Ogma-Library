using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.Views;
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

    [AvaloniaFact]
    public void MainWindow_RendersAndCapturesScreenshot_English()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        var window = new MainWindow { DataContext = new MainWindowViewModel(localization) };
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

        var window = new MainWindow { DataContext = new MainWindowViewModel(localization) };
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
        var viewModel = new MainWindowViewModel(localization);

        localization.SetCulture("en");
        Assert.Equal("Ogma Library", viewModel.Title);

        localization.SetCulture("fr");
        Assert.Equal("Bibliothèque Ogma", viewModel.Title);

        // No key resolves to the missing-key sentinel for the skeleton window.
        Assert.DoesNotContain("⟦", viewModel.Tagline, StringComparison.Ordinal);
    }
}
