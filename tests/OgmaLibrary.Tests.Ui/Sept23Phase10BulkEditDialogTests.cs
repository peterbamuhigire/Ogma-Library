using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Commands;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 10, task 10.8 (CAT-005, slice 5): the bulk-edit dialog shows a preview row per
/// book and keeps Apply disabled until a valid value changes a book; applying goes through the
/// command history and both the toast and the Activity Centre entry undo only their own edit.
/// </summary>
public sealed class Sept23Phase10BulkEditDialogTests
{
    [AvaloniaFact]
    public void Dialog_ShowsPreviewRows_AndApplyNeedsAChange()
    {
        var localization = new InMemoryLocalizationService();
        BookSummaryProjection[] books = [Book(1, "Ada"), Book(2, null)];
        var form = new BulkEditViewModel(books, localization);
        var dialog = new BulkEditDialog { DataContext = form, Width = 680, Height = 560 };
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        Button apply = Find<Button>(dialog, "BulkEdit.Apply");
        Assert.False(apply.IsEnabled);

        form.Value = "Ada";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, RowIds(dialog).Length);
        Assert.True(apply.IsEnabled); // book 2 has no author, so one book changes

        var unchanged = new BulkEditViewModel([Book(1, "Ada")], localization) { Value = "Ada" };
        Assert.False(unchanged.CanApply);
        Assert.False(unchanged.HasValidationError);

        form.SelectedField = form.Fields.Single(option => option.Field == BulkEditField.Year);
        form.Value = "12345";
        Dispatcher.UIThread.RunJobs();
        Assert.True(form.HasValidationError);
        Assert.False(apply.IsEnabled);
        Assert.True(Find<TextBlock>(dialog, "BulkEdit.Validation").IsVisible);

        dialog.Close();
    }

    [AvaloniaFact]
    public void Dialog_RowsCarryAutomationIdsAndAccessibleNames()
    {
        var localization = new InMemoryLocalizationService();
        var form = new BulkEditViewModel([Book(7, null)], localization) { Value = "Grace Hopper" };
        var dialog = new BulkEditDialog { DataContext = form };
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        TextBlock row = Find<TextBlock>(dialog, "BulkEdit.Row.book-007");
        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(row);
        Assert.Equal("BulkEdit.Row.book-007", peer.GetAutomationId());
        Assert.True(peer.IsControlElement(), "The row id must sit on an element UI Automation exposes.");
        string name = AutomationProperties.GetName(row) ?? string.Empty;
        Assert.Contains("Grace Hopper", name, StringComparison.Ordinal);
        Assert.Contains("Topic 007", name, StringComparison.Ordinal);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task Cancel_WritesNothing()
    {
        Fixture fixture = await Fixture.CreateAsync();
        BulkEditViewModel form = Assert.IsType<BulkEditViewModel>(fixture.Selection.CreateBulkEdit());
        form.Value = "New Author";

        // The dialog closed with Cancel: the form was never applied.
        await fixture.Selection.ApplyBulkEditAsync(form);

        Assert.Empty(fixture.Writes.Edits);
        Assert.Empty(fixture.Selection.EditLog!.Items);
        Assert.DoesNotContain(fixture.Notifier.Notifications, n => n.MessageKey.StartsWith("Catalogue.BulkEdit", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Apply_OffersUndoInToastAndActivity_AndUndoRestores()
    {
        Fixture fixture = await Fixture.CreateAsync();
        BulkEditViewModel form = Assert.IsType<BulkEditViewModel>(fixture.Selection.CreateBulkEdit());
        form.Value = "New Author";
        Assert.True(form.Apply());

        await fixture.Selection.ApplyBulkEditAsync(form);
        Dispatcher.UIThread.RunJobs();

        BulkEditCommand applied = Assert.Single(fixture.Writes.Edits);
        Assert.Equal("New Author", applied.AuthorToSet);
        Assert.Equal(["book-001", "book-002"], applied.BookIds.Order(StringComparer.Ordinal));
        UserNotification toast = Assert.Single(fixture.Notifier.Notifications, n => n.MessageKey == "Catalogue.BulkEdit.Applied");
        Assert.Equal(UserNotificationActionKind.Undo, toast.ActionKind);
        EditActivityItem entry = Assert.Single(fixture.Selection.EditLog!.Items);
        Assert.True(entry.CanUndo);

        // Undo from the Activity Centre.
        Assert.True(entry.UndoCommand.CanExecute(null));
        entry.UndoCommand.Execute(null);
        await WaitForAsync(() => fixture.Writes.Restores.Count == 1);
        Assert.True(entry.IsUndone);
        Assert.False(entry.CanUndo);

        // A stale toast must not undo anything else.
        await toast.Action!(CancellationToken.None);
        Assert.Single(fixture.Writes.Restores);
    }

    [AvaloniaFact]
    public async Task ToastUndo_OfAnOlderEdit_DoesNothingWhileANewerEditExists()
    {
        Fixture fixture = await Fixture.CreateAsync();
        UserNotification first = await ApplyAuthorAsync(fixture, "First");
        UserNotification second = await ApplyAuthorAsync(fixture, "Second");

        await first.Action!(CancellationToken.None);
        Assert.Empty(fixture.Writes.Restores);

        await second.Action!(CancellationToken.None);
        Assert.Single(fixture.Writes.Restores);
        Assert.Equal(1, fixture.Selection.EditLog!.Items.Count(item => item.IsUndone));
        Assert.True(fixture.Selection.EditLog.Items[1].CanUndo);
    }

    private static async Task<UserNotification> ApplyAuthorAsync(Fixture fixture, string author)
    {
        BulkEditViewModel form = Assert.IsType<BulkEditViewModel>(fixture.Selection.CreateBulkEdit());
        form.Value = author;
        Assert.True(form.Apply());
        fixture.Notifier.Notifications.Clear();
        await fixture.Selection.ApplyBulkEditAsync(form);
        Dispatcher.UIThread.RunJobs();
        return Assert.Single(fixture.Notifier.Notifications, n => n.MessageKey == "Catalogue.BulkEdit.Applied");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static T Find<T>(Window window, string automationId)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static string[] RowIds(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Select(border => AutomationProperties.GetAutomationId(border))
            .Where(id => id?.StartsWith("BulkEdit.Row.", StringComparison.Ordinal) == true)
            .Select(id => id!)
            .ToArray();

    private static BookSummaryProjection Book(int index, string? author) =>
        new(
            $"book-{index:D3}",
            $"Topic {index:D3}",
            author is null ? [] : [author],
            null,
            0,
            3,
            [],
            null,
            true,
            2000 + index);

    private sealed record Fixture(
        CatalogueSelectionViewModel Selection,
        FakeWriteService Writes,
        RecordingNotifier Notifier)
    {
        public static async Task<Fixture> CreateAsync()
        {
            var localization = new InMemoryLocalizationService();
            var readModel = new FakeReadModel([Book(1, null), Book(2, "Someone")]);
            var writes = new FakeWriteService();
            var notifier = new RecordingNotifier();
            var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
            var sidebar = new ShelfSidebarViewModel(readModel, writes, localization, catalogue.Filter);
            var membership = new ShelfMembershipViewModel(readModel, writes, sidebar, catalogue, notifier);
            var selection = new CatalogueSelectionViewModel(
                catalogue,
                membership,
                localization,
                notifier,
                writeService: writes,
                history: new CommandHistory(),
                editLog: new EditActivityLog(localization));
            await catalogue.LoadAsync();
            Dispatcher.UIThread.RunJobs();
            foreach (BookSummaryProjection book in catalogue.FilteredItems.ToList())
            {
                catalogue.SelectedBooks.Add(book);
            }

            Dispatcher.UIThread.RunJobs();
            Assert.True(selection.CanEdit);
            return new Fixture(selection, writes, notifier);
        }
    }

    private sealed class NullNavigation : IBookDetailNavigationService, IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingNotifier : IUserNotifier
    {
        public List<UserNotification> Notifications { get; } = [];

        public void Notify(UserNotification notification) => Notifications.Add(notification);
    }

    private sealed class FakeReadModel(IReadOnlyList<BookSummaryProjection> books) : ICatalogueReadModel
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

    private sealed class FakeWriteService : ICatalogueWriteService
    {
        public List<BulkEditCommand> Edits { get; } = [];

        public List<IReadOnlyList<BulkEditSnapshot>> Restores { get; } = [];

        public Task<string> CreateShelfAsync(string name, bool isSmart = false, string? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult("shelf-new");

        public Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddBookToShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateMetadataFieldAsync(string bookId, string fieldName, string? value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default)
        {
            Edits.Add(command);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(IReadOnlyList<string> bookIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BulkEditSnapshot>>([]);

        public Task RestoreBulkEditSnapshotAsync(IReadOnlyList<BulkEditSnapshot> snapshots, CancellationToken cancellationToken = default)
        {
            Restores.Add(snapshots);
            return Task.CompletedTask;
        }
    }
}
