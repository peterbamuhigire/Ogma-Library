using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Commands;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11 slice 3 (task 11.4): inline metadata editing in the real inspector view.
/// Keys go through the headless window's keyboard, so routing and handling match the app.
/// </summary>
public sealed class Phase11DetailFieldEditTests
{
    [AvaloniaFact]
    public async Task Enter_SavesTheEdit_AndShowsUndo()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm, RecordingWriteService writes) = await ShowAsync();

        TextBox editor = BeginEdit(view, "Title");
        editor.Text = "Renamed Title";
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("Title", (string?)"Renamed Title")], writes.Updates);
        Assert.False(editor.IsVisible);
        Button display = FindById<Button>(view, "Detail.Field.Title");
        Assert.True(display.IsVisible);
        Assert.Contains(display.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Renamed Title");
        Button undo = FindById<Button>(view, "Detail.Action.UndoFieldEdit");
        Assert.True(undo.IsEffectivelyVisible);
        Assert.Equal("Undo the change to Title", AutomationProperties.GetName(undo));
        Assert.True(vm.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Escape_CancelsTheEdit_WithoutWriting_AndKeepsTheInspectorOpen()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm, RecordingWriteService writes) = await ShowAsync();

        TextBox editor = BeginEdit(view, "Year");
        editor.Text = "1700";
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(writes.Updates);
        Assert.False(editor.IsVisible);
        DetailFieldViewModel year = Assert.Single(vm.EditableFields, f => f.FieldName == "Year");
        Assert.Equal(DetailFieldState.Display, year.State);
        Assert.Equal("2021", year.EditText);
        Assert.True(vm.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task InvalidYear_ShowsInlineError_AndKeepsTheEditorOpen()
    {
        (Window window, BookDetailView view, _, RecordingWriteService writes) = await ShowAsync();

        TextBox editor = BeginEdit(view, "Year");
        editor.Text = "20x1";
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(writes.Updates);
        Assert.True(editor.IsVisible);
        TextBlock error = FindById<TextBlock>(view, "Detail.Field.Year.Error");
        Assert.True(error.IsEffectivelyVisible);
        Assert.Equal("Enter a year from 1 to 9999.", error.Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ProvenanceButton_IsNamed_AndLoadsOnFirstOpen()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm, _) = await ShowAsync();

        Button info = FindById<Button>(view, "Detail.Field.Title.Provenance");
        Assert.Equal("Where Title came from", AutomationProperties.GetName(info));
        Assert.True(info.Focusable);
        Assert.IsType<Flyout>(info.Flyout);
        DetailFieldViewModel title = Assert.Single(vm.EditableFields, f => f.FieldName == "Title");
        Assert.False(title.IsProvenanceLoaded);

        info.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(title.IsProvenanceLoaded);
        Assert.Contains("Source: GoogleBooks", title.ProvenanceText, StringComparison.Ordinal);
        window.Close();
    }

    private static TextBox BeginEdit(BookDetailView view, string field)
    {
        FindById<Button>(view, "Detail.Field." + field).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        TextBox editor = FindById<TextBox>(view, "Detail.Field." + field + ".Editor");
        Assert.True(editor.IsVisible);
        Assert.True(editor.IsFocused);
        return editor;
    }

    private static T FindById<T>(BookDetailView view, string id)
        where T : Control =>
        Assert.Single(view.GetVisualDescendants().OfType<T>(), c => AutomationProperties.GetAutomationId(c) == id);

    private static async Task<(Window, BookDetailView, BookDetailViewModel, RecordingWriteService)> ShowAsync()
    {
        var reads = new MutableReadModel(Projection());
        var writes = new RecordingWriteService(reads);
        var vm = new BookDetailViewModel(
            reads,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            catalogueWriteService: writes,
            commandHistory: new CommandHistory());
        await vm.LoadBookAsync(reads.Detail.BookId);
        vm.IsVisible = true;
        vm.SelectedSection = InspectorSection.Details;
        var view = new BookDetailView { DataContext = vm, Width = 360 };
        var window = new Window { Width = 360, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, vm, writes);
    }

    private static BookDetailProjection Projection() => new(
        "P11-EDIT",
        "Structure and Interpretation",
        ["Ada Example"],
        2021,
        "9780306406157",
        null,
        3,
        0,
        null,
        "books/structure.pdf",
        "ab12",
        1_048_576,
        new ReadingProgressProjection("P11-EDIT", 0, 0, null, (int)ReadingStatus.Unread),
        0,
        [new MetadataFieldProjection("Title", "Structure and Interpretation", "GoogleBooks", 0.8, false)],
        IsPasswordProtected: false,
        IsAvailable: true);

    private sealed class MutableReadModel(BookDetailProjection detail) : ICatalogueReadModel
    {
        public BookDetailProjection Detail { get; set; } = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(Detail);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detail.ReadingProgress);
    }

    private sealed class RecordingWriteService(MutableReadModel reads) : ICatalogueWriteService
    {
        public List<(string Field, string? Value)> Updates { get; } = [];

        public Task UpdateMetadataFieldAsync(string bookId, string fieldName, string? value, CancellationToken cancellationToken = default)
        {
            Updates.Add((fieldName, value));
            if (fieldName == "Title")
            {
                reads.Detail = reads.Detail with { Title = value };
            }

            return Task.CompletedTask;
        }

        public Task<MetadataFieldSnapshot?> CaptureMetadataFieldSnapshotAsync(string bookId, string fieldName, CancellationToken cancellationToken = default) =>
            Task.FromResult<MetadataFieldSnapshot?>(new MetadataFieldSnapshot(bookId, fieldName, reads.Detail.Title, reads.Detail.Authors, false, null));

        public Task RestoreMetadataFieldSnapshotAsync(MetadataFieldSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> CreateShelfAsync(string name, bool isSmart = false, string? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult("shelf");

        public Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddBookToShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(IReadOnlyList<string> bookIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BulkEditSnapshot>>([]);

        public Task RestoreBulkEditSnapshotAsync(IReadOnlyList<BulkEditSnapshot> snapshots, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpReaderNavigation : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
