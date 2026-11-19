using System.Threading;
using System.Threading.Tasks;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase11;

/// <summary>
/// Sept-23 Phase 11 slice 6 (task 11.7): each inspector fixture maps to one explicit state and
/// the right primary action; relink and the deleted-while-open path re-evaluate the action.
/// </summary>
public sealed class InspectorStateTests
{
    private const string BookId = "state-book";
    private const string SecretPath = @"C:\Users\someone\Private\moved-book.pdf";

    public static TheoryData<string, InspectorState, PrimaryActionKind> Fixtures => new()
    {
        { "ready", InspectorState.Ready, PrimaryActionKind.Read },
        { "missing", InspectorState.MissingFile, PrimaryActionKind.LocateFile },
        { "password", InspectorState.PasswordProtected, PrimaryActionKind.Unlock },
        { "processing", InspectorState.Processing, PrimaryActionKind.Read },
        { "failed", InspectorState.ProcessingFailed, PrimaryActionKind.Read },
        { "failed-code", InspectorState.ProcessingFailed, PrimaryActionKind.Read },
        { "no-metadata", InspectorState.NoMetadata, PrimaryActionKind.Read },
        { "missing-and-locked", InspectorState.MissingFile, PrimaryActionKind.LocateFile },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_MapsToStateAndPrimaryAction(string fixture, InspectorState state, PrimaryActionKind action)
    {
        BookDetailViewModel vm = Create(Fixture(fixture), out _);

        await vm.LoadBookAsync(BookId);

        Assert.Equal(state, vm.State);
        Assert.Equal(action, vm.PrimaryAction);
        Assert.False(string.IsNullOrWhiteSpace(vm.PrimaryActionText));
    }

    [Fact]
    public void NoBook_IsNoneState()
    {
        BookDetailViewModel vm = Create((BookDetailProjection?)null, out _);

        Assert.Equal(InspectorState.None, vm.State);
    }

    [Fact]
    public async Task WhileLoading_StateIsLoading_ThenSettles()
    {
        var readModel = new GatedReadModel(Fixture("ready"));
        var vm = new BookDetailViewModel(readModel, new NoOpReader(), new InMemoryLocalizationService());

        Task load = vm.LoadBookAsync(BookId);
        Assert.Equal(InspectorState.Loading, vm.State);
        Assert.True(vm.IsLoadingState);
        Assert.False(vm.IsContentVisible);

        readModel.Release();
        await load;

        Assert.Equal(InspectorState.Ready, vm.State);
        Assert.True(vm.IsContentVisible);
    }

    [Fact]
    public async Task PasswordProtected_UnlockIsDisabledWithReason_AndForgetPasswordKept()
    {
        BookDetailViewModel vm = Create(Fixture("password"), out _, passwordProvider: new StubPasswordProvider());

        await vm.LoadBookAsync(BookId);

        Assert.False(vm.CanExecutePrimaryAction);
        Assert.False(string.IsNullOrWhiteSpace(vm.PasswordStateBodyText));
        Assert.True(vm.CanForgetPassword);
    }

    [Fact]
    public async Task ProcessingFailed_RetryRequeuesOcr()
    {
        var ocr = new RecordingOcrQueue();
        BookDetailViewModel vm = Create(Fixture("failed"), out _, ocr: ocr);
        await vm.LoadBookAsync(BookId);

        Assert.True(vm.CanRetryProcessing);
        await vm.RetryProcessingAsync();

        Assert.Equal(1, ocr.Calls);
        Assert.False(string.IsNullOrWhiteSpace(vm.OcrStatusText));
    }

    [Fact]
    public async Task ProcessingFailed_WithoutOcrQueue_CannotRetry()
    {
        BookDetailViewModel vm = Create(Fixture("failed"), out _);
        await vm.LoadBookAsync(BookId);

        Assert.False(vm.CanRetryProcessing);
    }

    [Fact]
    public async Task LocateFile_MatchingFile_RelinksAndRefreshesToReady()
    {
        var readModel = new StubReadModel(Fixture("missing"));
        var relink = new StubRelink(BookRelinkResult.Relinked, () => readModel.Detail = Fixture("ready"));
        BookDetailViewModel vm = Create(readModel, out _, relink: relink);
        vm.LocateFilePicker = _ => Task.FromResult<string?>(SecretPath);
        await vm.LoadBookAsync(BookId);

        await vm.ExecutePrimaryActionAsync();

        Assert.Equal(SecretPath, relink.LastPath);
        Assert.Equal(BookId, relink.LastBookId);
        Assert.Equal(InspectorState.Ready, vm.State);
        Assert.Equal(PrimaryActionKind.Read, vm.PrimaryAction);
        Assert.Equal(L("Catalogue.BookDetail.Relink.Relinked"), vm.RelinkStatusText);
    }

    [Theory]
    [InlineData(BookRelinkResult.HashMismatch)]
    [InlineData(BookRelinkResult.OutsideLibraryRoots)]
    [InlineData(BookRelinkResult.NotFound)]
    [InlineData(BookRelinkResult.Failed)]
    public async Task LocateFile_Refused_StaysMissingWithLocalisedReasonWithoutPath(BookRelinkResult outcome)
    {
        var relink = new StubRelink(outcome);
        BookDetailViewModel vm = Create(Fixture("missing"), out _, relink: relink);
        vm.LocateFilePicker = _ => Task.FromResult<string?>(SecretPath);
        await vm.LoadBookAsync(BookId);

        await vm.ExecutePrimaryActionAsync();

        Assert.Equal(InspectorState.MissingFile, vm.State);
        Assert.Equal(L($"Catalogue.BookDetail.Relink.{outcome}"), vm.RelinkStatusText);
        Assert.DoesNotContain("moved-book", vm.RelinkStatusText!, StringComparison.Ordinal);
        Assert.False(vm.IsRelinking);
    }

    [Fact]
    public async Task LocateFile_PickerCancelled_DoesNothing()
    {
        var relink = new StubRelink(BookRelinkResult.Relinked);
        BookDetailViewModel vm = Create(Fixture("missing"), out _, relink: relink);
        vm.LocateFilePicker = _ => Task.FromResult<string?>(null);
        await vm.LoadBookAsync(BookId);

        await vm.ExecutePrimaryActionAsync();

        Assert.Null(relink.LastPath);
        Assert.Null(vm.RelinkStatusText);
    }

    [Fact]
    public async Task LocateFile_ServiceThrows_ShowsFailedWithoutExceptionText()
    {
        var relink = new StubRelink(BookRelinkResult.Relinked) { Failure = new IOException($"Denied: {SecretPath}") };
        BookDetailViewModel vm = Create(Fixture("missing"), out _, relink: relink);
        vm.LocateFilePicker = _ => Task.FromResult<string?>(SecretPath);
        await vm.LoadBookAsync(BookId);

        await vm.ExecutePrimaryActionAsync();

        Assert.Equal(L("Catalogue.BookDetail.Relink.Failed"), vm.RelinkStatusText);
        Assert.False(vm.IsRelinking);
    }

    [Fact]
    public async Task DeletedWhileOpen_ReadFailure_SwitchesToMissingFileAndLocate()
    {
        var locator = new StubLocator(path: null);
        BookDetailViewModel vm = Create(Fixture("ready"), out RecordingReader reader, locator: locator);
        await vm.LoadBookAsync(BookId);

        await vm.OpenReaderAsync();

        Assert.Equal(0, reader.Calls);
        Assert.Equal(InspectorState.MissingFile, vm.State);
        Assert.Equal(PrimaryActionKind.LocateFile, vm.PrimaryAction);
        Assert.False(vm.CanOpenReader);
    }

    [Fact]
    public async Task DeletedWhileOpen_RefreshAfterFileReturns_RestoresRead()
    {
        var locator = new StubLocator(path: null);
        BookDetailViewModel vm = Create(Fixture("ready"), out _, locator: locator);
        await vm.LoadBookAsync(BookId);
        await vm.OpenReaderAsync();
        Assert.Equal(InspectorState.MissingFile, vm.State);

        locator.Path = SecretPath;
        await vm.RefreshLoadedBookAsync();

        Assert.Equal(InspectorState.Ready, vm.State);
        Assert.Equal(PrimaryActionKind.Read, vm.PrimaryAction);
    }

    [Fact]
    public async Task Refresh_CatalogueMarksFileMissing_SwitchesToLocate()
    {
        var readModel = new StubReadModel(Fixture("ready"));
        BookDetailViewModel vm = Create(readModel, out _);
        await vm.LoadBookAsync(BookId);

        readModel.Detail = Fixture("missing");
        await vm.RefreshLoadedBookAsync();

        Assert.Equal(InspectorState.MissingFile, vm.State);
        Assert.Equal(PrimaryActionKind.LocateFile, vm.PrimaryAction);
    }

    [Fact]
    public async Task StateTexts_AreLocalisedInFrench()
    {
        var localization = new InMemoryLocalizationService();
        BookDetailViewModel vm = Create(new StubReadModel(Fixture("missing")), out _, localization: localization);
        await vm.LoadBookAsync(BookId);
        string english = vm.MissingStateTitleText;

        localization.SetCulture("fr");

        Assert.NotEqual(english, vm.MissingStateTitleText);
        Assert.False(string.IsNullOrWhiteSpace(vm.LocateFileText));
    }

    private static string L(string key) => new InMemoryLocalizationService()[key];

    // ── Fixtures ───────────────────────────────────────────────────────────────

    private static BookDetailProjection Fixture(string name)
    {
        BookDetailProjection ready = new(
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
            ReadingProgress: null,
            Annotations: 0,
            MetadataFields: [],
            TextStatus: BookTextStatus.Searchable);
        return name switch
        {
            "ready" => ready,
            "missing" => ready with { IsAvailable = false },
            "password" => ready with { IsPasswordProtected = true },
            "processing" => ready with { TextStatus = BookTextStatus.OcrInProgress },
            "failed" => ready with { TextStatus = BookTextStatus.OcrFailed, OcrFailureCode = "engine_failed" },
            "failed-code" => ready with { TextStatus = BookTextStatus.ImageOnly, OcrFailureCode = "engine_failed" },
            "no-metadata" => ready with { Authors = [], Year = null },
            "missing-and-locked" => ready with { IsAvailable = false, IsPasswordProtected = true },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    private static BookDetailViewModel Create(
        BookDetailProjection? detail,
        out RecordingReader reader,
        IBookFileLocator? locator = null,
        IOcrJobQueueService? ocr = null,
        IPasswordProvider? passwordProvider = null,
        IBookRelinkService? relink = null) =>
        Create(new StubReadModel(detail), out reader, locator, ocr, passwordProvider, relink);

    private static BookDetailViewModel Create(
        StubReadModel readModel,
        out RecordingReader reader,
        IBookFileLocator? locator = null,
        IOcrJobQueueService? ocr = null,
        IPasswordProvider? passwordProvider = null,
        IBookRelinkService? relink = null,
        InMemoryLocalizationService? localization = null)
    {
        reader = new RecordingReader();
        return new BookDetailViewModel(
            readModel,
            reader,
            localization ?? new InMemoryLocalizationService(),
            ocrJobs: ocr,
            passwordProvider: passwordProvider,
            fileLocator: locator,
            bookRelink: relink);
    }

    private sealed class StubRelink(BookRelinkResult result, Action? onRelinked = null) : IBookRelinkService
    {
        public string? LastPath { get; private set; }

        public string? LastBookId { get; private set; }

        public Exception? Failure { get; init; }

        public Task<BookRelinkResult> RelinkAsync(string bookId, string candidatePath, CancellationToken cancellationToken = default)
        {
            LastBookId = bookId;
            LastPath = candidatePath;
            if (Failure is not null)
            {
                return Task.FromException<BookRelinkResult>(Failure);
            }

            if (result == BookRelinkResult.Relinked)
            {
                onRelinked?.Invoke();
            }

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingOcrQueue : IOcrJobQueueService
    {
        public int Calls { get; private set; }

        public Task<OcrQueueResult> QueueBookAsync(string bookId, string languageHint = "eng", CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new OcrQueueResult(true, false, 1, null));
        }
    }

    private sealed class StubPasswordProvider : IPasswordProvider
    {
        public Task<PasswordResult> GetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordResult.Cancelled());

        public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingReader : IReaderNavigationService
    {
        public int Calls { get; private set; }

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpReader : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubLocator(string? path) : IBookFileLocator
    {
        public string? Path { get; set; } = path;

        public Task<string?> LocateAsync(string bookId, CancellationToken ct) => Task.FromResult(Path);
    }

    private class StubReadModel(BookDetailProjection? detail) : ICatalogueReadModel
    {
        public BookDetailProjection? Detail { get; set; } = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public virtual Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
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

    private sealed class GatedReadModel(BookDetailProjection detail) : StubReadModel(detail)
    {
        private readonly TaskCompletionSource _gate = new();

        public void Release() => _gate.SetResult();

        public override async Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default)
        {
            await _gate.Task;
            return Detail;
        }
    }
}
