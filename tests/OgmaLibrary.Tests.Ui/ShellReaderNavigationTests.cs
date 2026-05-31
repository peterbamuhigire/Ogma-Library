using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Tests the production shell path from book detail navigation into the reader.
/// </summary>
public sealed class ShellReaderNavigationTests
{
    [AvaloniaFact]
    public async Task MainShell_OpenReaderAsync_OpensReaderAndHidesDetail()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new EmptyCatalogueReadModel();
        var writeService = new NoOpCatalogueWriteService();
        var filter = new CatalogueFilterViewModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization)
        {
            IsVisible = true,
        };
        var shelfSidebar = new ShelfSidebarViewModel(readModel, writeService, localization, filter);
        var sessionService = new FakeReaderSessionService();
        var reader = new ReaderViewModel(
            sessionService,
            new EmptyAnnotationService(),
            new EmptyBookmarkService(),
            new EmptyLayerService(),
            new EmptyCitationService(),
            new EmptyReadingMemoryService(),
            localization);

        var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            reader);

        await shell.OpenReaderAsync("book-001", pageHint: 4, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ShellView.Reader, shell.ActiveView);
        Assert.False(shell.BookDetail.IsVisible);
        Assert.True(shell.Reader?.IsOpen);
        Assert.Equal("book-001", sessionService.OpenedBookId);
        Assert.Equal(4, shell.Reader?.CurrentPageIndex);
    }

    [AvaloniaFact]
    public async Task MainShell_OpenPdfPathAsync_OpensReaderAndReportsMetadataQueued()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new EmptyCatalogueReadModel();
        var writeService = new NoOpCatalogueWriteService();
        var filter = new CatalogueFilterViewModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization);
        var shelfSidebar = new ShelfSidebarViewModel(readModel, writeService, localization, filter);
        var sessionService = new FakeReaderSessionService();
        var reader = new ReaderViewModel(
            sessionService,
            new EmptyAnnotationService(),
            new EmptyBookmarkService(),
            new EmptyLayerService(),
            new EmptyCitationService(),
            new EmptyReadingMemoryService(),
            localization);
        var directOpen = new FakeDirectPdfOpenService("book-direct");

        var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            reader,
            directPdfOpenService: directOpen);

        await shell.OpenPdfPathAsync(@"C:\fixtures\direct.pdf");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(@"C:\fixtures\direct.pdf", directOpen.OpenedPath);
        Assert.Equal(ShellView.Reader, shell.ActiveView);
        Assert.Equal("book-direct", sessionService.OpenedBookId);
        Assert.Equal("PDF opened in reader. Metadata extraction and enrichment are queued.", shell.StatusText);
    }

    private sealed class EmptyCatalogueReadModel : ICatalogueReadModel
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

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

    private sealed class FakeDirectPdfOpenService : IDirectPdfOpenService
    {
        private readonly string _bookId;

        public FakeDirectPdfOpenService(string bookId) => _bookId = bookId;

        public string? OpenedPath { get; private set; }

        public Task<string> OpenAsync(
            string absoluteFilePath,
            CancellationToken cancellationToken = default)
        {
            OpenedPath = absoluteFilePath;
            return Task.FromResult(_bookId);
        }
    }

    private sealed class FakeReaderSessionService : IReaderSessionService
    {
        public string? OpenedBookId { get; private set; }

        public ReaderSession? CurrentSession { get; private set; }

        public IPdfRenderer? CurrentRenderer => null;

        public Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct)
        {
            OpenedBookId = bookId;
            CurrentSession = new ReaderSession(
                bookId,
                "C:/fixtures/book.pdf",
                PageCount: 12,
                CurrentPageIndex: pageHint ?? 0,
                ScrollOffset: 0,
                ZoomMode.FitWidth,
                ZoomPercent: 100,
                DisplayMode.SinglePage);
            return Task.FromResult(CurrentSession);
        }

        public Task CloseAsync(CancellationToken ct)
        {
            CurrentSession = null;
            return Task.CompletedTask;
        }

        public Task NavigateToAsync(int pageIndex, double scrollOffset = 0.0)
        {
            CurrentSession = CurrentSession?.WithPage(pageIndex, scrollOffset);
            return Task.CompletedTask;
        }

        public void UpdateScrollOffset(double scrollOffset)
        {
        }
    }

    private sealed class EmptyBookmarkService : IBookmarkService
    {
        public Task<Bookmark> CreateAsync(
            string bookId,
            int pageIndex,
            string? label,
            CancellationToken cancellationToken) =>
            Task.FromResult(new Bookmark
            {
                Id = 1,
                BookId = bookId,
                PageIndex = pageIndex,
                Label = label,
                CreatedUtc = DateTimeOffset.UtcNow,
            });

        public Task RenameAsync(long bookmarkId, string newLabel, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(long bookmarkId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Bookmark>> GetForBookAsync(
            string bookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Bookmark>>(Array.Empty<Bookmark>());
    }

    private sealed class EmptyAnnotationService : IAnnotationService
    {
        public Task<AnnotationV2> CreateHighlightAsync(
            string bookId,
            string? layerId,
            IReadOnlyList<AnnotationRegion> regions,
            string color,
            string? quoteText,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AnnotationV2
            {
                Id = "annotation-001",
                BookId = bookId,
                LayerId = layerId,
                Kind = AnnotationKind.Highlight,
                Regions = regions,
                HighlightColor = color,
                QuoteText = quoteText,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            });

        public Task<AnnotationV2> CreateNoteAsync(
            string bookId,
            string? layerId,
            AnnotationRegion region,
            string noteText,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AnnotationV2
            {
                Id = "annotation-001",
                BookId = bookId,
                LayerId = layerId,
                Kind = AnnotationKind.Note,
                Regions = [region],
                NoteText = noteText,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            });

        public Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string annotationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AnnotationV2>> GetForPageAsync(
            string bookId,
            int pageIndex,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnnotationV2>>(Array.Empty<AnnotationV2>());
    }

    private sealed class EmptyLayerService : IAnnotationLayerService
    {
        private readonly List<AnnotationLayer> _layers = [];

        public Task<IReadOnlyList<AnnotationLayer>> GetLayersAsync(
            string bookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnnotationLayer>>(
                _layers.Where(l => l.BookId == bookId).ToList());

        public Task<AnnotationLayer> CreateLayerAsync(
            string bookId,
            string name,
            string color,
            CancellationToken cancellationToken)
        {
            var layer = new AnnotationLayer
            {
                Id = $"layer-{_layers.Count + 1}",
                BookId = bookId,
                Name = name,
                Color = color,
                IsVisible = true,
                SortOrder = _layers.Count,
            };
            _layers.Add(layer);
            return Task.FromResult(layer);
        }

        public Task RenameLayerAsync(string layerId, string newName, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SetVisibilityAsync(string layerId, bool isVisible, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string bookId, string layerId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MergeLayersAsync(
            string bookId,
            string sourceLayerId,
            string targetLayerId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class EmptyCitationService : ICitationService
    {
        public Task<CitationCard> CaptureAsync(
            string bookId,
            int pageIndex,
            string selectedText,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CitationCard(
                bookId,
                null,
                null,
                pageIndex + 1,
                selectedText));

        public Task<string> ExportAsync(CitationCard card, CancellationToken cancellationToken) =>
            Task.FromResult(Path.Combine(Path.GetTempPath(), "citation.txt"));
    }

    private sealed class EmptyReadingMemoryService : IReadingMemoryService
    {
        public Task<ReadingMemory> LoadAsync(string bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new ReadingMemory { BookId = bookId });

        public Task SaveAsync(ReadingMemory memory, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
