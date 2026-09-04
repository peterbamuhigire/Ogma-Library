using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Phase 20 proof for lazy detail-file, TOC, and provenance states.</summary>
public sealed class BookDetailFileAndProvenanceTests
{
    [AvaloniaFact]
    public async Task TocAndProvenance_AreLazyAndBounded()
    {
        var locator = new RecordingFileLocator("C:\\library\\book.pdf");
        var toc = new RecordingTocService(
        [
            new TocEntryRecord("Introduction", 0, 0),
            new TocEntryRecord("Methods", 4, 1),
        ]);
        var vm = CreateViewModel(locator, toc, CreateProjection("lazy-book"));

        await vm.LoadBookAsync("lazy-book");

        Assert.Empty(vm.TocRows);
        Assert.Empty(vm.ProvenanceRows);
        Assert.Equal(0, locator.Calls);
        Assert.Equal(0, toc.Calls);

        vm.LoadProvenance();
        Assert.True(vm.IsProvenanceLoaded);
        Assert.Contains("GoogleBooks", Assert.Single(vm.ProvenanceRows));

        await vm.LoadTocAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, locator.Calls);
        Assert.Equal(1, toc.Calls);
        Assert.Equal(["Introduction (p. 1)", "  Methods (p. 5)"], vm.TocRows);
        Assert.True(vm.IsTocLoaded);
        Assert.False(vm.ShowLoadTocButton);
    }

    [AvaloniaFact]
    public async Task MissingFile_RemainsUsefulAndDoesNotOpenOrParseAPath()
    {
        var locator = new RecordingFileLocator(null);
        var toc = new RecordingTocService([]);
        var reader = new RecordingReaderNavigation();
        var vm = new BookDetailViewModel(
            new SingleBookReadModel(CreateProjection("missing-book") with
            {
                RelativePath = "missing/book.pdf",
                IsAvailable = false,
            }),
            reader,
            new InMemoryLocalizationService(),
            fileLocator: locator,
            tocExtraction: toc);

        await vm.LoadBookAsync("missing-book");

        Assert.False(vm.CanOpenReader);
        Assert.Contains("unavailable", vm.FileAvailabilityText, StringComparison.OrdinalIgnoreCase);
        await vm.OpenReaderAsync();
        Assert.Equal(0, reader.Calls);

        await vm.LoadTocAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, locator.Calls);
        Assert.Equal(0, toc.Calls);
        Assert.True(vm.IsTocLoaded);
        Assert.True(vm.HasNoToc);
        Assert.Equal("Curation book", vm.Title);
    }

    private static BookDetailViewModel CreateViewModel(
        IBookFileLocator locator,
        ITocExtractionService toc,
        BookDetailProjection projection) =>
        new(
            new SingleBookReadModel(projection),
            new RecordingReaderNavigation(),
            new InMemoryLocalizationService(),
            fileLocator: locator,
            tocExtraction: toc);

    private static BookDetailProjection CreateProjection(string bookId) => new(
        bookId,
        "Curation book",
        ["Author"],
        2026,
        null,
        null,
        null,
        0,
        null,
        "book.pdf",
        "hash",
        10,
        null,
        0,
        [new MetadataFieldProjection("Title", "Curation book", "GoogleBooks", 0.95, false)],
        IsAvailable: true);

    private sealed class SingleBookReadModel(BookDetailProjection projection) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) => Task.FromResult<BookDetailProjection?>(projection);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) => Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class RecordingFileLocator(string? path) : IBookFileLocator
    {
        public int Calls { get; private set; }

        public Task<string?> LocateAsync(string bookId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(path);
        }
    }

    private sealed class RecordingTocService(IReadOnlyList<TocEntryRecord> entries) : ITocExtractionService
    {
        public int Calls { get; private set; }

        public Task<TocExtractionResult> ExtractAsync(string absoluteFilePath, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new TocExtractionResult(entries, TocExtractionQuality.Complete));
        }
    }

    private sealed class RecordingReaderNavigation : IReaderNavigationService
    {
        public int Calls { get; private set; }

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
