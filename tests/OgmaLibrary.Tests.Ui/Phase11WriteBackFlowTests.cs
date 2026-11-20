using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using static OgmaLibrary.Tests.Ui.Phase11ReviewFakes;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11 slice 5 (task 11.6): the two-step write-back consent dialog. Nothing is
/// written before the step-2 confirm, cancel discards the backup token, the original stays
/// restorable after the dialog closes, and failures show localised text.
/// </summary>
public sealed class Phase11WriteBackFlowTests
{
    private const string FilePath = "C:\\library\\books\\review.pdf";

    // ── Flow (view model) ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Phase11WriteBack_NoWriteBeforeStepTwoConfirm()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();

        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();

        Assert.Equal(WriteBackDialogStep.Review, dialog.Step);
        Assert.Equal("C:\\library\\.ogma\\backups\\review.pdf", dialog.BackupPath);
        Assert.Contains("New title", Assert.Single(dialog.DiffRows));
        Assert.Equal(0, writeBack.WriteCalls);

        dialog.Continue();
        Assert.Equal(WriteBackDialogStep.Confirm, dialog.Step);
        Assert.Equal(0, writeBack.WriteCalls);

        await dialog.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, writeBack.WriteCalls);
        Assert.Equal(WriteBackDialogStep.Result, dialog.Step);
        Assert.False(dialog.IsError);
        Assert.Equal("PDF metadata written successfully.", dialog.StatusText);
        Assert.True(dialog.CanRestore);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_PreparedWhenPrepareReturns_EvenIfIoCompletesOffTheUiThread()
    {
        // K129: the view opens the dialog only if HasPreparedWriteBack is true as soon as
        // PrepareWriteBackAsync returns. Real I/O resumes on the thread pool, and a posted UI
        // update left the flag false, so the dialog never opened in the real window.
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        writeBack.CompleteOffUiThread = true;

        await vm.PrepareWriteBackAsync();

        Assert.True(vm.HasPreparedWriteBack);
        Assert.Equal("C:\\library\\.ogma\\backups\\review.pdf", vm.CreateWriteBackDialog().BackupPath);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_CancelDiscardsTokenWithoutWriting()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        bool closeRequested = false;
        dialog.CloseRequested += (_, _) => closeRequested = true;

        dialog.Continue();
        dialog.Cancel();

        Assert.True(closeRequested);
        Assert.False(vm.HasPreparedWriteBack);
        Assert.False(vm.CanRestoreWriteBack);
        Assert.False(vm.HasWriteBackRestorePoint);
        Assert.Equal(0, writeBack.WriteCalls);
        Assert.Equal(0, writeBack.RestoreCalls);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_RestoreAfterDialogClosesCallsRestoreBackup()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        dialog.Continue();
        await dialog.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();

        dialog.HandleClosed();

        Assert.True(vm.HasWriteBackRestorePoint);
        Assert.True(vm.CanRestoreWriteBack);

        await vm.RestoreWriteBackAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, writeBack.RestoreCalls);
        Assert.Same(writeBack.LastPreparedToken, writeBack.LastRestoredToken);
        Assert.False(vm.HasWriteBackRestorePoint);
        Assert.Equal("The original PDF was restored from its backup.", vm.WriteBackStatusText);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_WriteReturningFalseSurfacesLocalisedError()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        writeBack.WriteResult = false;
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        dialog.Continue();

        await dialog.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WriteBackDialogStep.Result, dialog.Step);
        Assert.True(dialog.IsError);
        Assert.Equal("PDF writeback failed; the original was restored.", dialog.StatusText);
        Assert.False(vm.HasWriteBackRestorePoint);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_WriteThrowingShowsNoRawExceptionText()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        writeBack.WriteFailure = new UnauthorizedAccessException(RawSecret);
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        dialog.Continue();

        await dialog.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(dialog.IsError);
        Assert.DoesNotContain("RAW-SECRET", dialog.StatusText, StringComparison.Ordinal);
        Assert.Contains("could not be updated", dialog.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_ClosingBeforeConfirmDiscardsToken()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        dialog.Continue();

        dialog.HandleClosed();

        Assert.False(vm.HasPreparedWriteBack);
        Assert.Equal(0, writeBack.WriteCalls);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_FrenchStringsAreLocalised()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("fr");
        (BookDetailViewModel vm, _) = await CreateAsync(localization);
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();

        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();

        Assert.Equal("Vérifier les changements", dialog.ReviewStepTitle);
        Assert.Equal("Annuler", dialog.CancelText);
        Assert.Equal("Restaurer l'original", dialog.RestoreText);
    }

    // ── Headless dialog ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Phase11WriteBack_DialogStepsAreNamedAndCancelHasInitialFocus()
    {
        (BookDetailViewModel vm, _) = await CreateAsync();
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        var window = new WriteBackDialog { DataContext = dialog };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("WriteBack.Dialog", AutomationId(window));
        Button cancel = FindButton(window, "WriteBack.Cancel");
        Assert.True(cancel.IsVisible);
        Assert.True(cancel.IsFocused);
        Assert.True(FindButton(window, "WriteBack.Continue").IsVisible);
        Assert.False(FindButton(window, "WriteBack.Confirm").IsVisible);
        AssertAllVisibleButtonsNamed(window);

        dialog.Continue();
        Dispatcher.UIThread.RunJobs();

        Button confirm = FindButton(window, "WriteBack.Confirm");
        Assert.True(confirm.IsVisible);
        Assert.False(confirm.IsDefault);
        Assert.False(confirm.IsFocused);
        Assert.True(FindButton(window, "WriteBack.Back").IsVisible);
        AssertAllVisibleButtonsNamed(window);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_EscapeCancelsAndDiscardsToken()
    {
        (BookDetailViewModel vm, FakeWriteBack writeBack) = await CreateAsync();
        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        var window = new WriteBackDialog { DataContext = vm.CreateWriteBackDialog() };
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
        Assert.False(vm.HasPreparedWriteBack);
        Assert.Equal(0, writeBack.WriteCalls);
    }

    [AvaloniaFact]
    public async Task Phase11WriteBack_InspectorHasNoInlineBlockAndShowsRestoreUnderActivity()
    {
        (BookDetailViewModel vm, _) = await CreateAsync();
        vm.IsVisible = true;
        var view = new BookDetailView { DataContext = vm, Width = 360 };
        var window = new Window { Width = 360, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), button =>
            button.Content is TextBlock text && text.Text == "Confirm and write to PDF");

        await vm.PrepareWriteBackAsync();
        Dispatcher.UIThread.RunJobs();
        WriteBackDialogViewModel dialog = vm.CreateWriteBackDialog();
        dialog.Continue();
        await dialog.ConfirmAsync();
        dialog.HandleClosed();
        vm.SelectedSection = InspectorSection.Activity;
        Dispatcher.UIThread.RunJobs();

        Button restore = FindButton(view, "Detail.WriteBack.Restore");
        Assert.True(restore.IsEffectivelyVisible);
        Assert.Equal("Restore original", Avalonia.Automation.AutomationProperties.GetName(restore));
        window.Close();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static async Task<(BookDetailViewModel Vm, FakeWriteBack WriteBack)> CreateAsync(
        InMemoryLocalizationService? localization = null)
    {
        var writeBack = new FakeWriteBack();
        var reads = new MutableReadModel(Projection());
        var vm = new BookDetailViewModel(
            reads,
            new NoOpReaderNavigation(),
            localization ?? new InMemoryLocalizationService(),
            fileLocator: new FixedLocator(FilePath),
            writeBackService: writeBack);
        await vm.LoadBookAsync(reads.Detail.BookId);
        Dispatcher.UIThread.RunJobs();
        return (vm, writeBack);
    }

    private static string? AutomationId(Control control) =>
        Avalonia.Automation.AutomationProperties.GetAutomationId(control);

    private static Button FindButton(Visual root, string automationId) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => AutomationId(button) == automationId);

    private static void AssertAllVisibleButtonsNamed(Visual root)
    {
        foreach (Button button in root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(button)),
                $"Button {AutomationId(button)} has no accessible name.");
        }
    }

    private sealed class FixedLocator(string path) : IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) => Task.FromResult<string?>(path);
    }

    private sealed class FakeWriteBack : IMetadataWriteBackService
    {
        public bool WriteResult { get; set; } = true;
        public Exception? WriteFailure { get; set; }
        public int WriteCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public BackupToken? LastPreparedToken { get; private set; }
        public BackupToken? LastRestoredToken { get; private set; }

        /// <summary>Completes the backup on the thread pool, as real file I/O does (K129).</summary>
        public bool CompleteOffUiThread { get; set; }

        public async Task<BackupToken> PrepareBackupAsync(
            string bookId, string absoluteFilePath, CancellationToken cancellationToken = default)
        {
            if (CompleteOffUiThread)
            {
                await Task.Run(() => Thread.Sleep(20), cancellationToken).ConfigureAwait(false);
            }

            LastPreparedToken = new BackupToken("C:\\library\\.ogma\\backups\\review.pdf", absoluteFilePath, "abc");
            return LastPreparedToken;
        }

        public Task<WriteBackPlan?> GetWriteBackPlanAsync(
            string bookId, CancellationToken cancellationToken = default) => Task.FromResult<WriteBackPlan?>(null);

        public Task<IReadOnlyList<FieldDiff>> BuildDiffAsync(
            string absoluteFilePath,
            IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FieldDiff>>([new FieldDiff("Title", "Old title", "New title")]);

        public Task<bool> WriteAsync(
            string bookId,
            IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
            BackupToken backupToken,
            CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            return WriteFailure is null ? Task.FromResult(WriteResult) : Task.FromException<bool>(WriteFailure);
        }

        public Task<bool> RestoreBackupAsync(
            string bookId, BackupToken backupToken, CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            LastRestoredToken = backupToken;
            return Task.FromResult(true);
        }
    }
}
