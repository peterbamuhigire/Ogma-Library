using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11 slice 6 (task 11.7): every explicit inspector state renders its own named
/// template, and one frame per state is captured as evidence.
/// </summary>
public sealed class Phase11InspectorStateViewTests
{
    private static readonly string[] StateCardIds =
    [
        "Detail.State.MissingFile", "Detail.State.Password", "Detail.State.Processing", "Detail.State.Failed",
    ];

    [AvaloniaFact]
    public async Task Loading_ShowsTokenSkeleton_AndHidesContent()
    {
        var gate = new TaskCompletionSource<BookDetailProjection?>();
        BookDetailViewModel vm = NewViewModel(new GatedReadModel(gate.Task));
        (Window window, BookDetailView view) = Show(vm, 360);
        vm.IsVisible = true;

        Task load = vm.LoadBookAsync("P11-BOOK");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(InspectorState.Loading, vm.State);
        StackPanel skeleton = FindById<StackPanel>(view, "Detail.State.Loading");
        Assert.True(skeleton.IsEffectivelyVisible);
        Assert.Equal("Loading book details", AutomationProperties.GetName(skeleton));
        Border[] blocks = skeleton.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("skeleton")).ToArray();
        Assert.True(blocks.Length >= 5);
        Assert.True(view.TryFindResource("Brush.Surface.Sunken", view.ActualThemeVariant, out object? sunken));
        Assert.NotNull(sunken);
        Assert.All(blocks, b => Assert.Equal(sunken, b.Background));
        Assert.False(FindById<Control>(view, "Detail.Header").IsEffectivelyVisible);
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<ProgressBar>(), p => p.IsEffectivelyVisible);
        Capture(window, "loading");

        gate.SetResult(Projection());
        await load;
        Dispatcher.UIThread.RunJobs();
        Assert.False(skeleton.IsEffectivelyVisible);
        Assert.True(FindById<Control>(view, "Detail.Header").IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task MissingFile_ShowsMessage_AndLocateFileIsThePrimaryAction()
    {
        (Window window, BookDetailView view, _) = await ShowAsync(Projection() with { IsAvailable = false });

        AssertOnlyCard(view, "Detail.State.MissingFile");
        Assert.Equal("File not found", AutomationProperties.GetName(FindById<Border>(view, "Detail.State.MissingFile")));
        Assert.True(FindById<TextBlock>(view, "Detail.State.MissingFile.Body").IsEffectivelyVisible);
        Button primary = FindById<Button>(view, "Detail.Action.Read");
        Assert.Equal("Locate file…", AutomationProperties.GetName(primary));
        Assert.True(primary.IsEnabled);
        Capture(window, "missing-file");
        window.Close();
    }

    [AvaloniaFact]
    public async Task PasswordProtected_ExplainsDisabledUnlock_AndKeepsForgetPassword()
    {
        (Window window, BookDetailView view, _) = await ShowAsync(Projection() with { IsPasswordProtected = true });

        AssertOnlyCard(view, "Detail.State.Password");
        Assert.True(FindById<TextBlock>(view, "Detail.State.Password.Reason").IsEffectivelyVisible);
        Button forget = FindById<Button>(view, "Detail.State.Password.Forget");
        Assert.True(forget.IsEffectivelyVisible);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(forget)));
        Button primary = FindById<Button>(view, "Detail.Action.Read");
        Assert.Equal("Unlock", AutomationProperties.GetName(primary));
        Assert.False(primary.IsEnabled);
        Capture(window, "password");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Processing_ShowsLiveStatus_AndReadStaysPrimary()
    {
        (Window window, BookDetailView view, _) = await ShowAsync(
            Projection() with { TextStatus = BookTextStatus.OcrInProgress });

        AssertOnlyCard(view, "Detail.State.Processing");
        TextBlock status = FindById<TextBlock>(view, "Detail.State.Processing.Status");
        Assert.True(status.IsEffectivelyVisible);
        Assert.False(string.IsNullOrWhiteSpace(status.Text));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
        Assert.Equal("Read", AutomationProperties.GetName(FindById<Button>(view, "Detail.Action.Read")));
        Capture(window, "processing");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ProcessingFailed_ShowsReason_AndRetry()
    {
        (Window window, BookDetailView view, _) = await ShowAsync(
            Projection() with { TextStatus = BookTextStatus.OcrFailed, OcrFailureCode = "engine_failed" },
            new RecordingOcrQueue());

        AssertOnlyCard(view, "Detail.State.Failed");
        TextBlock reason = FindById<TextBlock>(view, "Detail.State.Failed.Reason");
        Assert.True(reason.IsEffectivelyVisible);
        Assert.False(string.IsNullOrWhiteSpace(reason.Text));
        Button retry = FindById<Button>(view, "Detail.State.Failed.Retry");
        Assert.Equal("Retry", AutomationProperties.GetName(retry));
        Assert.True(retry.IsEnabled);
        Capture(window, "processing-failed");
        window.Close();
    }

    [AvaloniaFact]
    public async Task NoMetadata_ShowsTheNoMetadataCard()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm) = await ShowAsync(
            Projection() with { Authors = [], Year = null, Isbn = null });

        Assert.Equal(InspectorState.NoMetadata, vm.State);
        AssertOnlyCard(view, null);
        Assert.True(FindById<Border>(view, "Detail.State.NoMetadata").IsEffectivelyVisible);
        Capture(window, "no-metadata");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Ready_ShowsNoStateCard()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm) = await ShowAsync(Projection());

        Assert.Equal(InspectorState.Ready, vm.State);
        AssertOnlyCard(view, null);
        Assert.False(FindById<Control>(view, "Detail.RelinkStatus").IsEffectivelyVisible);
        Capture(window, "ready");
        window.Close();
    }

    [AvaloniaFact]
    public async Task View_InstallsTheLocateFilePicker()
    {
        (Window window, _, BookDetailViewModel vm) = await ShowAsync(Projection() with { IsAvailable = false });

        Assert.NotNull(vm.LocateFilePicker);
        window.Close();
    }

    private static void AssertOnlyCard(Visual view, string? visibleId)
    {
        foreach (string id in StateCardIds)
        {
            Assert.Equal(id == visibleId, FindById<Border>(view, id).IsEffectivelyVisible);
        }

        Assert.False(FindById<StackPanel>(view, "Detail.State.Loading").IsEffectivelyVisible);
    }

    private static void Capture(Window window, string state)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string dir = Path.Combine(AppContext.BaseDirectory, "artifacts", "phase-11");
        Directory.CreateDirectory(dir);
        frame!.Save(Path.Combine(dir, $"inspector-state-{state}.png"));
    }

    private static T FindById<T>(Visual view, string id)
        where T : Control =>
        Assert.Single(view.GetVisualDescendants().OfType<T>(), c => AutomationProperties.GetAutomationId(c) == id);

    private static BookDetailViewModel NewViewModel(ICatalogueReadModel readModel, IOcrJobQueueService? ocr = null) =>
        new(
            readModel,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            ocrJobs: ocr,
            fileLocator: new StubLocator());

    private static (Window Window, BookDetailView View) Show(BookDetailViewModel vm, double width)
    {
        var view = new BookDetailView { DataContext = vm, Width = width };
        var window = new Window { Width = width, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static async Task<(Window Window, BookDetailView View, BookDetailViewModel Vm)> ShowAsync(
        BookDetailProjection projection, IOcrJobQueueService? ocr = null)
    {
        BookDetailViewModel vm = NewViewModel(new GatedReadModel(Task.FromResult<BookDetailProjection?>(projection)), ocr);
        await vm.LoadBookAsync(projection.BookId);
        vm.IsVisible = true;
        (Window window, BookDetailView view) = Show(vm, 360);
        return (window, view, vm);
    }

    private static BookDetailProjection Projection() => new(
        "P11-BOOK",
        "Structure and Interpretation",
        ["Ada Example"],
        2021,
        "9780306406157",
        null,
        3,
        0,
        null,
        "books/structure.pdf",
        "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12",
        1_048_576,
        new ReadingProgressProjection("P11-BOOK", 0, 0, null, (int)ReadingStatus.Unread),
        2,
        [],
        IsAvailable: true,
        TextStatus: BookTextStatus.Searchable);

    private sealed class GatedReadModel(Task<BookDetailProjection?> detail) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            detail;

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class RecordingOcrQueue : IOcrJobQueueService
    {
        public Task<OcrQueueResult> QueueBookAsync(
            string bookId,
            string languageHint = "eng",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubLocator : OgmaLibrary.Application.Reader.IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) => Task.FromResult<string?>("books/structure.pdf");
    }

    private sealed class NoOpReaderNavigation : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
