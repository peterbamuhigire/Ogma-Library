using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Headless render of the real library health dashboard (Sept-23 Phase 10, META-008). It replaces
/// the earlier inline fake panel: the shipped <see cref="LibraryHealthView"/> is bound to a
/// <see cref="LibraryHealthViewModel"/> over fakes, its tiles and actions are checked through their
/// automation ids, and the frame is captured to <c>artifacts/screenshots/health-en.png</c>.
/// </summary>
public sealed class HealthDashboardRenderTests
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

    /// <summary>Renders the real dashboard, checks its tiles and captures a screenshot.</summary>
    [AvaloniaFact]
    public async Task HealthDashboard_RendersTheRealView_AndCapturesScreenshot_English()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var health = new LibraryHealthViewModel(
            new BooksReadModel(),
            new OneFailedJobService(),
            new CatalogueFilterViewModel(),
            localization,
            new ProvidersOn());
        await health.LoadAsync();

        var view = new LibraryHealthView { DataContext = health };
        var window = new Window { Width = 1100, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        health.ActivateTile(LibraryHealthTileKind.FailedJobs);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Dictionary<string, Button> tiles = window.GetVisualDescendants().OfType<Button>()
            .Where(button => AutomationProperties.GetAutomationId(button)?.StartsWith("Health.Tile.", StringComparison.Ordinal) == true)
            .ToDictionary(button => AutomationProperties.GetAutomationId(button)!);
        Assert.Equal(8, tiles.Count);
        Assert.Contains("1", AutomationProperties.GetName(tiles["Health.Tile.MissingAuthor"]), StringComparison.Ordinal);
        Assert.All(tiles.Values, tile =>
        {
            Assert.DoesNotContain("⟦", AutomationProperties.GetName(tile), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(tile) as string));
        });

        Control failedJobs = window.GetVisualDescendants().OfType<Control>()
            .Single(control => AutomationProperties.GetAutomationId(control) == "Health.FailedJobs");
        Assert.True(failedJobs.IsEffectivelyVisible);
        Control batch = window.GetVisualDescendants().OfType<Control>()
            .Single(control => AutomationProperties.GetAutomationId(control) == "Health.Batch");
        Assert.True(batch.IsEffectivelyVisible);

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        string screenshotPath = Path.Combine(ArtifactsDir, "health-en.png");
        frame!.Save(screenshotPath);
        DocScreenshots.Publish(screenshotPath, "health-en.png");
        Assert.True(File.Exists(screenshotPath), $"Screenshot not saved to {screenshotPath}");
        window.Close();
    }

    private sealed class OneFailedJobService : ILibraryHealthService
    {
        public Task<LibraryHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibraryHealthSnapshot(
                [], [], [], [],
                [new FailedJobEntry(7, "Enrich", "b", "provider_timeout", DateTimeOffset.UtcNow)],
                DateTimeOffset.UtcNow,
                [new BatchEnrichmentRunEntry("batch-1", 4, 2, 0, 1, 1, 0)]));

        public Task RetryJobAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResumeBatchEnrichmentAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> ExportFailedJobsCsvAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);
    }

    /// <summary>Two books; the second has no author, so the missing-author tile shows 1.</summary>
    private sealed class BooksReadModel : ICatalogueReadModel
    {
        private static readonly BookSummaryProjection[] Books =
        [
            new("a", "Alpha Atlas", ["Ann Author"], null, 0, null, [], null, true, 2020),
            new("b", "Bravo Book", [], null, 0, null, [], null, true, 2021),
        ];

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in Books)
            {
                if (!CatalogueCriteriaEvaluator.Matches(book, filter))
                {
                    continue;
                }

                yield return book;
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class ProvidersOn : ICapabilityState
    {
        public event EventHandler? Changed { add { } remove { } }

        public bool AreMetadataProvidersEnabled => true;

        public bool IsAiConfigured => false;

        public bool IsClassroomHostEnabled => false;

        public bool IsClassroomClientConnected => false;

        public bool IsClassroomAvailable => false;

        public bool IsShelf3DAvailable => false;
    }
}
