using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 06 (T06.6, K13): the status bar reports processing tasks with their own
/// counters (never the scan's file counters), never shows N &gt; M, and returns to a
/// finished state when only jobs waiting for a missing capability remain.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaFact]
    public void ProcessingStatus_ShowsTasksNotFiles_AndNeverExceedsTotal()
    {
        var localization = new InMemoryLocalizationService();
        var processing = new FakeProcessingProgress();
        MainShellViewModel shell = CreateProcessingShell(localization, processing);

        processing.Publish(new ProcessingSnapshot(40, 2, 17, 0, 0, Empty, 5));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Preparing books: 17 of 59 tasks", shell.StatusText);
        Assert.True(shell.IsProcessing);

        // Parked (waiting) jobs are not part of the busy total.
        processing.Publish(new ProcessingSnapshot(0, 1, 58, 0, 16, Waiting(16), 13));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Preparing books: 58 of 59 tasks", shell.StatusText);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ProcessingStatus_OnlyProviderWaitingJobsLeft_IsFinishedNotBusy()
    {
        var localization = new InMemoryLocalizationService();
        var processing = new FakeProcessingProgress();
        MainShellViewModel shell = CreateProcessingShell(localization, processing);
        await shell.InitializeAsync();

        processing.Publish(new ProcessingSnapshot(0, 0, 59, 0, 16, Waiting(16), 13));
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.IsProcessing);
        Assert.DoesNotContain("Preparing", shell.StatusText, StringComparison.Ordinal);
        Assert.Equal(localization["MainWindow.Status.Ready"], shell.StatusText); // empty library
        shell.Dispose();
    }

    [AvaloniaFact]
    public void ProcessingStatus_FrenchWording()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("fr");
        var processing = new FakeProcessingProgress();
        MainShellViewModel shell = CreateProcessingShell(localization, processing);

        processing.Publish(new ProcessingSnapshot(3, 1, 2, 0, 0, Empty, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Préparation des livres : 2 sur 6 tâches", shell.StatusText);
        shell.Dispose();
    }

    private static readonly IReadOnlyDictionary<string, int> Empty = new Dictionary<string, int>();

    private static Dictionary<string, int> Waiting(int count) =>
        new Dictionary<string, int> { [JobCapabilities.SemanticEmbeddings] = count };

    private static MainShellViewModel CreateProcessingShell(
        InMemoryLocalizationService localization,
        IProcessingProgressService processing)
    {
        var filter = new OgmaLibrary.App.ViewModels.Catalogue.CatalogueFilterViewModel();
        var readModel = new EmptyCatalogueReadModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization);
        var shelfSidebar = new ShelfSidebarViewModel(readModel, new NoOpCatalogueWriteService(), localization, filter);
        return new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            processingProgress: processing);
    }

    private sealed class FakeProcessingProgress : IProcessingProgressService
    {
        public ProcessingSnapshot CurrentSnapshot { get; private set; } = ProcessingSnapshot.Idle;

        public event EventHandler<ProcessingSnapshot>? ProgressChanged;

        public void Publish(ProcessingSnapshot snapshot)
        {
            CurrentSnapshot = snapshot;
            ProgressChanged?.Invoke(this, snapshot);
        }

        public void NotifyChanged()
        {
        }

        public Task<ProcessingSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentSnapshot);
    }
}
