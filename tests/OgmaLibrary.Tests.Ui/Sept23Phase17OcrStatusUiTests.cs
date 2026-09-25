using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 17 headless UI: honest text-status badges, the "Needs OCR" filter, the Activity
/// Centre "make scanned books searchable" banner, and localised OCR failure reasons.
/// </summary>
public sealed class Sept23Phase17OcrStatusUiTests
{
    [AvaloniaFact]
    public void CatalogueGrid_ScannedBook_ShowsImageOnlyNotIndexed_AndOcrBookShowsConfidence()
    {
        // Fails before: the scanned book's finished index job put "Indexed" on its card (K21).
        CatalogueViewModel vm = LoadCatalogue(
            Book("scan", "Scanned Pamphlet", BookTextStatus.ImageOnly),
            Book("ocr", "Scanned Handout", BookTextStatus.OcrText, confidence: 0.91),
            Book("text", "Algorithms Explained", BookTextStatus.Searchable));
        var window = new Window { Width = 1280, Height = 800, Content = new CatalogueGridView { DataContext = vm } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            string?[] visibleTexts = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.IsEffectivelyVisible)
                .Select(text => text.Text)
                .ToArray();
            Assert.Contains("Scanned, needs OCR", visibleTexts);
            Assert.Contains("OCR text", visibleTexts);
            Assert.Contains("91 %", visibleTexts);
            Assert.Contains("Searchable", visibleTexts);
            Assert.DoesNotContain("Indexed", visibleTexts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CatalogueFilter_NeedsOcr_ShowsOnlyBooksOcrWouldHelp()
    {
        CatalogueViewModel vm = LoadCatalogue(
            Book("scan", "Scanned Pamphlet", BookTextStatus.ImageOnly),
            Book("part", "Mixed Report", BookTextStatus.PartlySearchable),
            Book("fail", "Blurry Scan", BookTextStatus.OcrFailed),
            Book("ocr", "Scanned Handout", BookTextStatus.OcrText, confidence: 0.9),
            Book("text", "Algorithms Explained", BookTextStatus.Searchable));

        vm.Filter.NeedsOcrOnly = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["fail", "part", "scan"], vm.FilteredItems.Select(item => item.BookId).Order(StringComparer.Ordinal));
        Assert.True(vm.Filter.HasActiveFilters);
        vm.Filter.ClearAll();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(5, vm.FilteredItems.Count);
    }

    [AvaloniaFact]
    public async Task ActivityCentre_Banner_OffersAndQueuesOcrForScannedBooks()
    {
        var queue = new StubOcrQueue { NeedingOcr = 2 };
        using var vm = new ActivityCentreViewModel(new StubRuntime(), new InMemoryLocalizationService(), queue);

        await vm.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.HasOcrSuggestion);
        Assert.Equal("Make 2 scanned book(s) searchable", vm.MakeSearchableLabel);
        Assert.Equal("2 scanned book(s) cannot be searched yet.", vm.OcrSuggestionText);

        queue.NeedingOcr = 0;
        int queued = await vm.MakeScannedBooksSearchableAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, queued);
        Assert.Equal(1, queue.BulkCalls);
        Assert.False(vm.HasOcrSuggestion);
        Assert.Equal("OCR queued for 2 book(s).", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task ActivityCentre_OcrFailure_ShowsLocalisedReason_InEnglishAndFrench()
    {
        var localization = new InMemoryLocalizationService();
        using var vm = new ActivityCentreViewModel(new StubRuntime(), localization);

        await vm.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        ActivityJobDisplayItem failed = vm.Jobs.Single(job => job.JobType == "OcrJob");

        Assert.Contains("OCR language pack is not installed", failed.StateText, StringComparison.Ordinal);
        Assert.DoesNotContain("ocr_", failed.StateText, StringComparison.Ordinal);

        localization.SetCulture("fr");
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("module de langue OCR", vm.Jobs.Single(job => job.JobType == "OcrJob").StateText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task BookDetail_ShowsTextStatus_QualityAndOcrFailureReason()
    {
        var readModel = new DetailReadModel(new BookDetailProjection(
            BookId: "scan",
            Title: "Scanned Pamphlet",
            Authors: [],
            Year: null,
            Isbn: null,
            Doi: null,
            Rating: null,
            Status: 0,
            CoverRelativePath: null,
            RelativePath: "Edge Cases/scan.pdf",
            Sha256Hash: null,
            SizeBytes: null,
            ReadingProgress: null,
            Annotations: 0,
            MetadataFields: [],
            TextStatus: BookTextStatus.OcrFailed,
            TextQuality: 0,
            OcrFailureCode: OcrFailureCodes.Timeout));
        var vm = new BookDetailViewModel(readModel, new NullNavigation(), new InMemoryLocalizationService());

        await vm.LoadBookAsync("scan");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("OCR failed", vm.TextStatusText);
        Assert.Equal("Text quality: 0 % of pages readable", vm.TextQualityText);
        Assert.True(vm.HasOcrFailure);
        Assert.Equal(
            "OCR did not finish: a page took too long to read. Try again when the computer is less busy.",
            vm.OcrFailureText);
    }

    private static BookSummaryProjection Book(string id, string title, BookTextStatus status, double? confidence = null) =>
        new(
            BookId: id,
            Title: title,
            Authors: ["Author"],
            CoverRelativePath: null,
            Status: 0,
            Rating: null,
            ShelfIds: [],
            ReadingProgressPct: null,
            IsAvailable: true,
            Year: null,
            Processing: new CatalogueProcessingProjection(
                SearchBookIndexStatus.Indexed,
                SearchEmbeddingStatus.NotEmbedded,
                0,
                IsOcrDerived: status == BookTextStatus.OcrText,
                TextStatus: status,
                TextQuality: status == BookTextStatus.Searchable ? 1 : 0,
                OcrConfidence: confidence));

    private static CatalogueViewModel LoadCatalogue(params BookSummaryProjection[] books)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var vm = new CatalogueViewModel(new SummaryReadModel(books), new NullNavigation(), localization);
        Task load = vm.LoadAsync(CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        load.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        return vm;
    }

    private sealed class SummaryReadModel(IReadOnlyList<BookSummaryProjection> books) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in books)
            {
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

    private sealed class DetailReadModel(BookDetailProjection detail) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(detail);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class NullNavigation : IBookDetailNavigationService, IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubOcrQueue : IOcrJobQueueService
    {
        public int NeedingOcr { get; set; }

        public int BulkCalls { get; private set; }

        public Task<OcrQueueResult> QueueBookAsync(string bookId, string languageHint = "eng", CancellationToken cancellationToken = default) =>
            Task.FromResult(new OcrQueueResult(true, false, 1, null));

        public Task<int> CountBooksNeedingOcrAsync(CancellationToken cancellationToken = default) => Task.FromResult(NeedingOcr);

        public Task<int> QueueBooksNeedingOcrAsync(string languageHint = "eng", int maxBooks = 500, CancellationToken cancellationToken = default)
        {
            BulkCalls++;
            return Task.FromResult(2);
        }
    }

    private sealed class StubRuntime : IJobRuntimeService
    {
        public Task<JobRuntimeDiagnostics> GetDiagnosticsAsync(int recentJobLimit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult(new JobRuntimeDiagnostics(
                new JobRuntimeMetrics(
                    DateTimeOffset.UtcNow,
                    PendingCount: 0,
                    RunningCount: 0,
                    CompletedCount: 1,
                    FailedCount: 1,
                    CancelledCount: 0,
                    DeadLetterCount: 0,
                    PausedCount: 0,
                    TotalAttempts: 1,
                    ActiveByJobType: new Dictionary<string, int>()),
                [
                    new JobRuntimeDiagnostic(7, "OcrJob", JobRuntimeStatus.Failed, 1, OcrFailureCodes.MissingLanguageData, null, DateTimeOffset.UtcNow),
                    new JobRuntimeDiagnostic(8, "SearchExtraction", JobRuntimeStatus.Completed, 1, null, null, DateTimeOffset.UtcNow),
                ]));

        public Task RetryFailedAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CancelPendingAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default) => Task.FromResult("{}");

        public Task<JobLease?> ClaimNextAsync(IReadOnlyCollection<string> jobTypes, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CompleteAsync(long jobId, string workerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RenewAsync(long jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task FailAsync(long jobId, string workerId, JobFailure failure, int maxAttempts = 3, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
