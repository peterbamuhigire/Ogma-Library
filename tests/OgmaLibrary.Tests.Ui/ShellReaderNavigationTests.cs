using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.Views.Reader;
using OgmaLibrary.App.Views.Settings;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Tests.Reader;
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

    [AvaloniaFact]
    public async Task MainShell_OpenPdfPathAsync_WithRealPdf_RegistersAndOpensReaderSession()
    {
        string dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-shell-real-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);

        await using ServiceProvider services = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddIngestionPipeline(dataDirectory)
            .AddSingleton<IBookFileLocator, BookFileLocator>()
            .BuildServiceProvider();

        try
        {
            await services.GetRequiredService<CatalogueMigrator>().ApplyAsync(CancellationToken.None);

            var localization = new InMemoryLocalizationService();
            var readModel = services.GetRequiredService<ICatalogueReadModel>();
            var writeService = new NoOpCatalogueWriteService();
            var filter = new CatalogueFilterViewModel();
            var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
            var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization);
            var shelfSidebar = new ShelfSidebarViewModel(readModel, writeService, localization, filter);
            var rendererFactory = new MockPdfRendererFactory(ReaderTestPdfFixture.PageCount);
            using var progressService = new ReadingProgressService(
                services.GetRequiredService<IReadingProgressRepository>());
            using var cache = new PageRenderCache(
                rendererFactory,
                new StopwatchBenchmarkContext());
            using var sessionService = new ReaderSessionService(
                rendererFactory,
                progressService,
                services.GetRequiredService<IBookFileLocator>(),
                cache);
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
                reader,
                directPdfOpenService: services.GetRequiredService<IDirectPdfOpenService>());

            await shell.OpenPdfPathAsync(ReaderTestPdfFixture.PdfPath);
            await WaitForAsync(() => shell.ActiveView == ShellView.Reader && reader.IsOpen);

            Assert.Equal(ShellView.Reader, shell.ActiveView);
            Assert.True(reader.IsOpen);
            Assert.Equal(ReaderTestPdfFixture.PageCount, reader.PageCount);
            Assert.Equal(ReaderTestPdfFixture.PdfPath, sessionService.CurrentSession?.FilePath);
            Assert.Single(catalogue.FilteredItems);
            Assert.Equal("PDF opened in reader. Metadata extraction and enrichment are queued.", shell.StatusText);

            shell.Dispose();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(dataDirectory))
                {
                    Directory.Delete(dataDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for Windows file-handle timing.
            }
        }
    }

    [AvaloniaFact]
    public async Task MainShell_OpenSharingSettingsAsync_RefreshesHostAndActivatesSettingsRoute()
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
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var hostSharing = new HostSharingViewModel(host, settings);

        var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            hostSharing: hostSharing);

        await shell.OpenSharingSettingsAsync();

        Assert.Equal(ShellView.SharingSettings, shell.ActiveView);
        Assert.True(shell.IsSharingSettingsActive);
        Assert.False(shell.BookDetail.IsVisible);
        Assert.Equal(1, host.StatusRequests);
        Assert.Equal("Running on :7473", hostSharing.StatusText);
        Assert.True(hostSharing.CanShare);
    }

    [AvaloniaFact]
    public async Task MainShell_HostConnectionSucceeded_ReloadsCatalogueAndReturnsToCatalogue()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new MutableCatalogueReadModel
        {
            Summary = new BookSummaryProjection(
                BookId: "host-book-1",
                Title: "Classroom Algebra",
                Authors: ["A. Teacher"],
                CoverRelativePath: null,
                Status: 0,
                Rating: null,
                ShelfIds: [],
                ReadingProgressPct: null,
                IsAvailable: true,
                Year: 2026),
        };
        var writeService = new NoOpCatalogueWriteService();
        var filter = new CatalogueFilterViewModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization)
        {
            IsVisible = true,
        };
        var shelfSidebar = new ShelfSidebarViewModel(readModel, writeService, localization, filter);
        var hostSharing = new HostSharingViewModel(
            new FakeLibraryHostService(),
            new FakeHostModeSettingsRepository(),
            new AcceptingJoinParser(),
            new SuccessfulConnectionService())
        {
            JoinLink = "ogma-lan://classroom/join",
            ProfileDisplayName = "Amina",
            AcceptFirstUseTrust = true,
        };

        var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            hostSharing: hostSharing);
        await shell.OpenSharingSettingsAsync();

        await hostSharing.ConnectToHostAsync();
        await WaitForAsync(() =>
            shell.ActiveView == ShellView.Catalogue &&
            catalogue.FilteredItems.Count == 1 &&
            catalogue.FilteredItems[0].Title == "Classroom Algebra");

        Assert.True(shell.IsCatalogueActive);
        Assert.False(shell.BookDetail.IsVisible);
        Assert.Equal("Connected to Classroom Host", hostSharing.ClientConnectionStatusText);

        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task SharingSettingsView_RendersHostControls()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var hostSharing = new HostSharingViewModel(host, settings);
        await hostSharing.RefreshAsync();

        var window = new Window
        {
            Width = 980,
            Height = 720,
            Content = new SharingSettingsView { DataContext = hostSharing },
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Running on :7473", hostSharing.StatusText);
            Assert.Contains("ogma-lan://", hostSharing.ManualJoinUri, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task MainShell_BackgroundCompletion_RefreshesCatalogueAndLoadedDetail()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new MutableCatalogueReadModel();
        var writeService = new NoOpCatalogueWriteService();
        var filter = new CatalogueFilterViewModel();
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var bookDetail = new BookDetailViewModel(readModel, new NullNavigation(), localization);
        var shelfSidebar = new ShelfSidebarViewModel(readModel, writeService, localization, filter);
        var progress = new FakeScanProgressService();

        var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            scanProgress: progress);

        await catalogue.LoadAsync();
        await bookDetail.LoadBookAsync("book-direct");
        Dispatcher.UIThread.RunJobs();

        Assert.Null(catalogue.FilteredItems[0].Title);
        Assert.Null(bookDetail.Title);

        readModel.Summary = readModel.Summary with
        {
            Title = "Extracted Runtime Title",
            Authors = ["Runtime Author"],
        };
        readModel.Detail = readModel.Detail with
        {
            Title = "Extracted Runtime Title",
            Authors = ["Runtime Author"],
        };

        progress.SetPhase(ScanPhase.Complete);

        await WaitForAsync(() =>
            catalogue.FilteredItems[0].Title == "Extracted Runtime Title" &&
            bookDetail.Title == "Extracted Runtime Title");

        Assert.Equal(["Runtime Author"], catalogue.FilteredItems[0].Authors);
        Assert.Equal("Runtime Author", bookDetail.AuthorsDisplay);

        shell.Dispose();
    }

    [AvaloniaFact]
    public void SplitView_Route_Exists_ShowsV2Placeholder()
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
        var splitView = new SplitViewViewModel(localization);
        var shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            splitView: splitView);

        shell.OpenSplitViewScaffold();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ShellView.SplitView, shell.ActiveView);
        Assert.True(shell.IsSplitViewActive);
        Assert.False(shell.IsReaderActive);
        Assert.False(shell.IsCatalogueActive);
        Assert.False(shell.BookDetail.IsVisible);
        Assert.Equal("Split view is coming in V2.", splitView.PlaceholderText);

        var window = new Window
        {
            Width = 900,
            Height = 520,
            Content = new SplitViewView { DataContext = splitView },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(window.Content);
        window.Close();
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

    private sealed class MutableCatalogueReadModel : ICatalogueReadModel
    {
        public BookSummaryProjection Summary { get; set; } = new(
            BookId: "book-direct",
            Title: null,
            Authors: [],
            CoverRelativePath: null,
            Status: 0,
            Rating: null,
            ShelfIds: [],
            ReadingProgressPct: null,
            IsAvailable: true,
            Year: null);

        public BookDetailProjection Detail { get; set; } = new(
            BookId: "book-direct",
            Title: null,
            Authors: [],
            Year: null,
            Isbn: null,
            Doi: null,
            Rating: null,
            Status: 0,
            CoverRelativePath: null,
            RelativePath: "direct.pdf",
            Sha256Hash: null,
            SizeBytes: null,
            ReadingProgress: null,
            Annotations: 0,
            MetadataFields: []);

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return Summary;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(bookId == Detail.BookId ? Detail : null);

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

    private sealed class AcceptingJoinParser : IClassroomJoinParser
    {
        private static readonly ClassroomJoinRequest Request = new(
            "127.0.0.1",
            7473,
            new string('b', 64),
            DisplayName: "Classroom Host",
            EnrollmentCode: "ABC12345");

        public ClassroomJoinRequest Parse(string payload) => Request;

        public bool TryParse(string payload, out ClassroomJoinRequest? request, out string? errorMessage)
        {
            request = Request;
            errorMessage = null;
            return true;
        }
    }

    private sealed class SuccessfulConnectionService : IClassroomConnectionService
    {
        public Task<ClassroomConnectionResult> ConnectAsync(
            ClassroomConnectionRequest request,
            CancellationToken cancellationToken = default)
        {
            var connection = new ClassroomHostConnection(
                request.JoinRequest,
                "session-token",
                new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero));
            var profile = new ClassroomProfile(
                Guid.NewGuid(),
                request.ProfileDisplayName ?? "Guest",
                request.UseGuestProfile ? ClassroomRole.Guest : ClassroomRole.Student,
                request.UseGuestProfile);
            return Task.FromResult(new ClassroomConnectionResult(
                IsConnected: true,
                HostTrustState.Trusted,
                profile,
                connection));
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

    private sealed class FakeScanProgressService : IScanProgressService
    {
        public ScanProgressSnapshot CurrentSnapshot { get; private set; } =
            new(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);

        public event EventHandler<ScanProgressSnapshot>? ProgressChanged;

        public void SetPhase(ScanPhase phase)
        {
            CurrentSnapshot = CurrentSnapshot with
            {
                Phase = phase,
                IsCancellable = phase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets,
            };
            ProgressChanged?.Invoke(this, CurrentSnapshot);
        }

        public void IncrementDiscovered()
        {
            CurrentSnapshot = CurrentSnapshot with { FilesDiscovered = CurrentSnapshot.FilesDiscovered + 1 };
            ProgressChanged?.Invoke(this, CurrentSnapshot);
        }

        public void IncrementCompleted()
        {
            CurrentSnapshot = CurrentSnapshot with { FilesCompleted = CurrentSnapshot.FilesCompleted + 1 };
            ProgressChanged?.Invoke(this, CurrentSnapshot);
        }

        public void IncrementFailed()
        {
            CurrentSnapshot = CurrentSnapshot with { FilesFailed = CurrentSnapshot.FilesFailed + 1 };
            ProgressChanged?.Invoke(this, CurrentSnapshot);
        }

        public void Reset()
        {
            CurrentSnapshot = new ScanProgressSnapshot(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);
            ProgressChanged?.Invoke(this, CurrentSnapshot);
        }
    }

    private sealed class FakeHostModeSettingsRepository : IHostModeSettingsRepository
    {
        public HostModeSettings Settings { get; private set; } = new(
            IsEnabled: true,
            Port: 7473,
            ContentMode: HostContentDeliveryMode.PageRender,
            DisplayName: "Ogma Test Library");

        public Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibraryHostService : ILibraryHostService
    {
        private LibraryHostStatus _status = new(
            LibraryHostState.Running,
            Port: 7473,
            ConnectedClientCount: 3,
            CertificateFingerprint: new string('a', 64),
            ErrorMessage: null,
            HostAddress: "127.0.0.1",
            EnrollmentCode: "ABC12345");

        public int StatusRequests { get; private set; }

        public Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            StatusRequests++;
            return Task.FromResult(_status);
        }

        public Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_status);

        public Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default)
        {
            _status = _status with
            {
                State = LibraryHostState.Stopped,
                HostAddress = null,
                CertificateFingerprint = null,
                EnrollmentCode = null,
                ConnectedClientCount = 0,
            };
            return Task.FromResult(_status);
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25, timeout.Token);
        }
    }
}
