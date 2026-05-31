using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Proves the production main window shell renders and culture-switch bindings update.
/// </summary>
public sealed class SkeletonRenderTests
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

    private static MainShellViewModel CreateViewModel(InMemoryLocalizationService localization)
    {
        var readModel = new EmptyCatalogueReadModel();
        var writeService = new NoOpCatalogueWriteService();
        var filter = new CatalogueFilterViewModel();
        var nav = new NullNavigation();
        var catalogue = new CatalogueViewModel(readModel, nav, localization);
        var detail = new BookDetailViewModel(readModel, nav, localization);
        var shelves = new ShelfSidebarViewModel(readModel, writeService, localization, filter);
        return new MainShellViewModel(
            localization,
            catalogue,
            detail,
            shelves,
            null,
            new NullLibrarySettingsService(),
            new NullIngestionOrchestrator(),
            new NullScanProgressService());
    }

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
        Assert.Equal("Bibliotheque Ogma", RemoveDiacritics(viewModel.Title));

        Assert.DoesNotContain("\u27E6", viewModel.EmptyStateBody, StringComparison.Ordinal);

        viewModel.Dispose();
    }

    [AvaloniaFact]
    public void BookDetailViewModel_ReadingMemorySummary_DisplaysTruncatedInsight()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        string insight = string.Concat(
            "A compact summary should preserve the important argument while ",
            "truncating long reader notes for the detail panel.");
        var detail = new BookDetailProjection(
            "book-001",
            "Phase 09 Book",
            ["A. Reader"],
            Year: 2026,
            Isbn: null,
            Doi: null,
            Rating: null,
            Status: 0,
            CoverRelativePath: null,
            RelativePath: "phase-09.pdf",
            Sha256Hash: null,
            SizeBytes: null,
            ReadingProgress: null,
            Annotations: 3,
            MetadataFields: [],
            ReadingMemory: new ReadingMemorySummaryProjection(
                Disposition: 4,
                KeyInsight: insight,
                UpdatedAtUtc: DateTimeOffset.UtcNow));
        var viewModel = new BookDetailViewModel(
            new EmptyCatalogueReadModel(detail),
            new NullNavigation(),
            localization);

        viewModel.LoadBookAsync("book-001", CancellationToken.None).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.HasReadingMemorySummary);
        Assert.Equal("4/5", viewModel.ReadingMemoryDispositionDisplay);
        Assert.Equal(80, viewModel.ReadingMemoryKeyInsightExcerpt.Length);
        Assert.EndsWith("...", viewModel.ReadingMemoryKeyInsightExcerpt);
        Assert.Equal(3, viewModel.AnnotationCount);
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
    }

    private sealed class NullLibrarySettingsService : ILibrarySettingsService
    {
        public Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task SetExcludedFoldersAsync(
            IReadOnlyList<string> excludedFolders,
            CancellationToken cancellationToken = default) =>
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

        private void RaiseProgressChanged() => ProgressChanged?.Invoke(this, CurrentSnapshot);
    }

    private sealed class EmptyCatalogueReadModel : ICatalogueReadModel
    {
        private readonly BookDetailProjection? _detail;

        public EmptyCatalogueReadModel(BookDetailProjection? detail = null)
        {
            _detail = detail;
        }

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_detail?.BookId == bookId ? _detail : null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class NoOpCatalogueWriteService : ICatalogueWriteService
    {
        public Task<string> CreateShelfAsync(
            string name,
            bool isSmart = false,
            string? query = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("shelf-001");

        public Task RenameShelfAsync(
            string shelfId,
            string newName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddBookToShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateMetadataFieldAsync(
            string bookId,
            string fieldName,
            string? value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullNavigation :
        IBookDetailNavigationService,
        IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OpenReaderAsync(
            string bookId,
            int? pageHint = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
