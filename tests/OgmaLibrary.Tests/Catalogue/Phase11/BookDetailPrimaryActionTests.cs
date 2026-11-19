using System.Threading;
using System.Threading.Tasks;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase11;

/// <summary>
/// Sept-23 Phase 11 slices 1 (tasks 11.2 and 11.8): the inspector's single state-driven
/// primary action and the awaited, error-surfacing Read path.
/// </summary>
public sealed class BookDetailPrimaryActionTests
{
    private const string BookId = "book-11";
    private const string SecretPath = @"C:\Users\someone\Private\secret-book.pdf";

    // ── State fixtures: one per primary-action kind ─────────────────────────────

    [Fact]
    public void NoBook_PrimaryActionIsNone()
    {
        BookDetailViewModel vm = CreateVm(detail: null, out _);

        Assert.Equal(PrimaryActionKind.None, vm.PrimaryAction);
        Assert.False(vm.CanExecutePrimaryAction);
    }

    [Fact]
    public async Task UnreadBook_PrimaryActionIsRead()
    {
        BookDetailViewModel vm = CreateVm(Detail(progress: null), out _);

        await vm.LoadBookAsync(BookId);

        Assert.Equal(PrimaryActionKind.Read, vm.PrimaryAction);
        Assert.Equal("Read", vm.PrimaryActionText);
        Assert.True(vm.CanExecutePrimaryAction);
    }

    [Fact]
    public async Task InProgressBook_PrimaryActionIsContinueReadingWithOneBasedPage()
    {
        // CurrentPage is the persisted zero-based page index (ReadingProgressRepository).
        BookDetailViewModel vm = CreateVm(Detail(progress: Progress(currentPageIndex: 42)), out _);

        await vm.LoadBookAsync(BookId);

        Assert.Equal(PrimaryActionKind.ContinueReading, vm.PrimaryAction);
        Assert.Equal("Continue reading · p. 43", vm.PrimaryActionText);
    }

    [Fact]
    public async Task MissingFile_PrimaryActionIsLocateFile()
    {
        BookDetailViewModel vm = CreateVm(Detail(progress: Progress(10), isAvailable: false), out _);

        await vm.LoadBookAsync(BookId);

        Assert.Equal(PrimaryActionKind.LocateFile, vm.PrimaryAction);
        Assert.Equal("Locate file", vm.PrimaryActionText);
    }

    [Fact]
    public async Task PasswordProtectedBook_PrimaryActionIsUnlock()
    {
        BookDetailViewModel vm = CreateVm(Detail(progress: null, isPasswordProtected: true), out _);

        await vm.LoadBookAsync(BookId);

        Assert.Equal(PrimaryActionKind.Unlock, vm.PrimaryAction);
        Assert.Equal("Unlock", vm.PrimaryActionText);
    }

    [Fact]
    public async Task PrimaryActionText_FollowsCultureChange()
    {
        var localization = new InMemoryLocalizationService();
        BookDetailViewModel vm = CreateVm(Detail(progress: Progress(4)), out _, localization: localization);
        await vm.LoadBookAsync(BookId);

        localization.SetCulture("fr");

        Assert.Equal("Reprendre la lecture · p. 5", vm.PrimaryActionText);
    }

    // ── Awaited Read (11.8) ─────────────────────────────────────────────────────

    [Fact]
    public async Task OpenReader_FileMissingOnDisk_DoesNotCallReaderAndShowsLocalisedError()
    {
        var locator = new StubLocator(path: null);
        BookDetailViewModel vm = CreateVm(Detail(progress: null), out RecordingReader reader, locator: locator);
        await vm.LoadBookAsync(BookId);
        vm.IsVisible = true;

        await vm.OpenReaderAsync();

        Assert.Equal(0, reader.Calls);
        Assert.Equal(1, locator.Calls);
        Assert.Equal(vm.OpenFileMissingText, vm.OpenErrorText);
        Assert.True(vm.HasOpenError);
        Assert.True(vm.IsVisible);
        Assert.False(vm.IsOpeningReader);
    }

    [Fact]
    public async Task OpenReader_CatalogueMarksFileUnavailable_DoesNotCallReader()
    {
        BookDetailViewModel vm = CreateVm(Detail(progress: null, isAvailable: false), out RecordingReader reader);
        await vm.LoadBookAsync(BookId);

        await vm.OpenReaderAsync();

        Assert.Equal(0, reader.Calls);
        Assert.Equal(vm.OpenFileMissingText, vm.OpenErrorText);
    }

    [Fact]
    public async Task OpenReader_ReaderThrows_ShowsLocalisedMessageWithoutExceptionText()
    {
        BookDetailViewModel vm = CreateVm(
            Detail(progress: null),
            out RecordingReader reader,
            locator: new StubLocator(SecretPath),
            readerFailure: new IOException($"Access denied: {SecretPath}"));
        await vm.LoadBookAsync(BookId);

        await vm.OpenReaderAsync();

        Assert.Equal(1, reader.Calls);
        Assert.NotNull(vm.OpenErrorText);
        Assert.Equal(vm.OpenFailedText, vm.OpenErrorText);
        Assert.DoesNotContain("Access denied", vm.OpenErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-book", vm.OpenErrorText, StringComparison.Ordinal);
        Assert.False(vm.IsOpeningReader);
    }

    [Fact]
    public async Task OpenReader_Cancelled_PropagatesWithoutError()
    {
        BookDetailViewModel vm = CreateVm(
            Detail(progress: null),
            out _,
            readerFailure: new OperationCanceledException());
        await vm.LoadBookAsync(BookId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vm.OpenReaderAsync());

        Assert.Null(vm.OpenErrorText);
        Assert.False(vm.IsOpeningReader);
    }

    [Fact]
    public async Task OpenReader_Success_ClearsPreviousErrorAndCallsReaderOnce()
    {
        var locator = new StubLocator(path: null);
        BookDetailViewModel vm = CreateVm(Detail(progress: null), out RecordingReader reader, locator: locator);
        await vm.LoadBookAsync(BookId);
        await vm.OpenReaderAsync();
        Assert.NotNull(vm.OpenErrorText);

        locator.Path = SecretPath;
        await vm.OpenReaderAsync();

        Assert.Null(vm.OpenErrorText);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(BookId, reader.LastBookId);
    }

    [Fact]
    public async Task ReportReaderOpenFailed_ForLoadedBook_ShowsLocalisedError()
    {
        BookDetailViewModel vm = CreateVm(Detail(progress: null), out _);
        await vm.LoadBookAsync(BookId);

        vm.ReportReaderOpenFailed(BookId);
        vm.ReportReaderOpenFailed("another-book");

        Assert.Equal(vm.OpenFailedText, vm.OpenErrorText);
    }

    // ── ExecutePrimaryActionAsync dispatch ──────────────────────────────────────

    [Fact]
    public async Task ExecutePrimaryAction_ContinueReading_OpensReader()
    {
        BookDetailViewModel vm = CreateVm(Detail(progress: Progress(9)), out RecordingReader reader);
        await vm.LoadBookAsync(BookId);

        await vm.ExecutePrimaryActionAsync();

        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task ExecutePrimaryAction_LocateFileAndUnlock_DoNotOpenReaderAndExplain()
    {
        BookDetailViewModel missing = CreateVm(Detail(progress: null, isAvailable: false), out RecordingReader missingReader);
        await missing.LoadBookAsync(BookId);
        await missing.ExecutePrimaryActionAsync();

        BookDetailViewModel locked = CreateVm(Detail(progress: null, isPasswordProtected: true), out RecordingReader lockedReader);
        await locked.LoadBookAsync(BookId);
        await locked.ExecutePrimaryActionAsync();

        Assert.Equal(0, missingReader.Calls);
        Assert.Equal(0, lockedReader.Calls);
        Assert.False(string.IsNullOrWhiteSpace(missing.OpenErrorText));
        Assert.False(string.IsNullOrWhiteSpace(locked.OpenErrorText));
    }

    [Fact]
    public async Task ChangingBook_ClearsOpenError()
    {
        var readModel = new StubReadModel(Detail(progress: null, isAvailable: false));
        var vm = new BookDetailViewModel(readModel, new RecordingReader(null), new InMemoryLocalizationService());
        await vm.LoadBookAsync(BookId);
        await vm.OpenReaderAsync();
        Assert.NotNull(vm.OpenErrorText);

        readModel.Detail = Detail(progress: null) with { BookId = "book-other" };
        await vm.LoadBookAsync("book-other");

        Assert.Null(vm.OpenErrorText);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static BookDetailViewModel CreateVm(
        BookDetailProjection? detail,
        out RecordingReader reader,
        IBookFileLocator? locator = null,
        Exception? readerFailure = null,
        InMemoryLocalizationService? localization = null)
    {
        reader = new RecordingReader(readerFailure);
        return new BookDetailViewModel(
            new StubReadModel(detail),
            reader,
            localization ?? new InMemoryLocalizationService(),
            fileLocator: locator);
    }

    private static ReadingProgressProjection Progress(int currentPageIndex) =>
        new(BookId, currentPageIndex, 12.5, DateTimeOffset.UtcNow.AddDays(-1), Status: 1);

    private static BookDetailProjection Detail(
        ReadingProgressProjection? progress,
        bool isAvailable = true,
        bool isPasswordProtected = false) =>
        new(
            BookId: BookId,
            Title: "Synthetic Title",
            Authors: ["Synthetic Author"],
            Year: 2020,
            Isbn: null,
            Doi: null,
            Rating: null,
            Status: 0,
            CoverRelativePath: null,
            RelativePath: "books/synthetic.pdf",
            Sha256Hash: "abc123",
            SizeBytes: 2048,
            ReadingProgress: progress,
            Annotations: 0,
            MetadataFields: [],
            IsPasswordProtected: isPasswordProtected,
            IsAvailable: isAvailable);

    private sealed class RecordingReader(Exception? failure) : IReaderNavigationService
    {
        public int Calls { get; private set; }

        public string? LastBookId { get; private set; }

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastBookId = bookId;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class StubLocator(string? path) : IBookFileLocator
    {
        public string? Path { get; set; } = path;

        public int Calls { get; private set; }

        public Task<string?> LocateAsync(string bookId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Path);
        }
    }

    private sealed class StubReadModel(BookDetailProjection? detail) : ICatalogueReadModel
    {
        public BookDetailProjection? Detail { get; set; } = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detail);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
