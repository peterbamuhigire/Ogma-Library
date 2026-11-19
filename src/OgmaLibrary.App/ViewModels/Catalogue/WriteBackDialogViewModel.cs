using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>The steps of the write-back consent dialog.</summary>
public enum WriteBackDialogStep
{
    /// <summary>Step 1: review the field changes and the backup location.</summary>
    Review,

    /// <summary>Step 2: explicit confirmation before the PDF is changed.</summary>
    Confirm,

    /// <summary>The outcome of the write, with Restore original.</summary>
    Result,
}

/// <summary>
/// Two-step consent for writing catalogue metadata into a PDF (Sept-23 Phase 11 slice 5,
/// task 11.6). Step 1 shows the diff and the backup location with Cancel as the safe choice;
/// step 2 asks for an explicit confirmation with no preselected destructive default. Nothing is
/// written before <see cref="ConfirmAsync"/>. The state itself lives on the owning
/// <see cref="BookDetailViewModel"/>, so the restore point outlives the dialog.
/// </summary>
public sealed class WriteBackDialogViewModel : INotifyPropertyChanged
{
    private readonly BookDetailViewModel _owner;
    private readonly ILocalizationService _localization;
    private WriteBackDialogStep _step = WriteBackDialogStep.Review;
    private bool _closed;

    /// <summary>Initializes a new instance of <see cref="WriteBackDialogViewModel"/>.</summary>
    /// <param name="owner">The inspector that holds the prepared write-back.</param>
    /// <param name="localization">The localisation service.</param>
    public WriteBackDialogViewModel(BookDetailViewModel owner, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(localization);
        _owner = owner;
        _localization = localization;
        _owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the dialog should close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The current step.</summary>
    public WriteBackDialogStep Step
    {
        get => _step;
        private set
        {
            if (_step != value)
            {
                _step = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsReviewStep));
                OnPropertyChanged(nameof(IsConfirmStep));
                OnPropertyChanged(nameof(IsResultStep));
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepIndicatorText));
                OnPropertyChanged(nameof(CanRestore));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>True on step 1.</summary>
    public bool IsReviewStep => _step == WriteBackDialogStep.Review;

    /// <summary>True on step 2.</summary>
    public bool IsConfirmStep => _step == WriteBackDialogStep.Confirm;

    /// <summary>True once the write has run.</summary>
    public bool IsResultStep => _step == WriteBackDialogStep.Result;

    /// <summary>Field-level changes, one plain-language row per field.</summary>
    public ObservableCollection<string> DiffRows => _owner.WriteBackDiffRows;

    /// <summary>Absolute path of the backup copy of the original file.</summary>
    public string? BackupPath => _owner.WriteBackBackupPath;

    /// <summary>True while the write or restore is running.</summary>
    public bool IsBusy => _owner.IsWritingWriteBack;

    /// <summary>True when step 2 may write.</summary>
    public bool CanConfirm => _owner.CanConfirmWriteBack;

    /// <summary>True on the result step while the original can be restored.</summary>
    public bool CanRestore => IsResultStep && _owner.CanRestoreWriteBack;

    /// <summary>True when the result is a failure.</summary>
    public bool IsError => IsResultStep && _owner.IsWriteBackError;

    /// <summary>Localised outcome text on the result step.</summary>
    public string? StatusText => IsResultStep ? _owner.WriteBackStatusText : null;

    /// <summary>Localised window title.</summary>
    public string Title => T("Catalogue.BookDetail.WriteBack.DialogTitle");

    /// <summary>Localised step-1 title.</summary>
    public string ReviewStepTitle => T("Catalogue.BookDetail.WriteBack.ReviewStep");

    /// <summary>Localised step-2 title.</summary>
    public string ConfirmStepTitle => T("Catalogue.BookDetail.WriteBack.ConfirmStep");

    /// <summary>Localised title of the current step.</summary>
    public string StepTitle => _step switch
    {
        WriteBackDialogStep.Review => ReviewStepTitle,
        WriteBackDialogStep.Confirm => ConfirmStepTitle,
        _ => T("Catalogue.BookDetail.WriteBack.ResultStep"),
    };

    /// <summary>Localised "Step n of 2" indicator; empty on the result step.</summary>
    public string StepIndicatorText => _step == WriteBackDialogStep.Result
        ? string.Empty
        : string.Format(
            _localization.CurrentCulture,
            T("Catalogue.BookDetail.WriteBack.StepFormat"),
            _step == WriteBackDialogStep.Review ? 1 : 2,
            2);

    /// <summary>Localised step-1 explanation.</summary>
    public string ReviewIntroText => T("Catalogue.BookDetail.WriteBack.ReviewIntro");

    /// <summary>Localised label above the backup location.</summary>
    public string BackupLabelText => T("Catalogue.BookDetail.WriteBack.BackupLabel");

    /// <summary>Localised step-2 question.</summary>
    public string ConfirmIntroText => T("Catalogue.BookDetail.WriteBack.ConfirmIntro");

    /// <summary>Localised Cancel label.</summary>
    public string CancelText => T("Catalogue.BookDetail.WriteBack.CancelShort");

    /// <summary>Localised Continue label.</summary>
    public string ContinueText => T("Catalogue.BookDetail.WriteBack.Continue");

    /// <summary>Localised Back label.</summary>
    public string BackText => T("Catalogue.BookDetail.WriteBack.Back");

    /// <summary>Localised confirm label.</summary>
    public string ConfirmText => T("Catalogue.BookDetail.WriteBack.Confirm");

    /// <summary>Localised Restore original label.</summary>
    public string RestoreText => _owner.RestoreOriginalText;

    /// <summary>Localised Close label.</summary>
    public string CloseText => T("Catalogue.BookDetail.WriteBack.Close");

    /// <summary>Moves from review to confirmation. Writes nothing.</summary>
    public void Continue()
    {
        if (IsReviewStep)
        {
            Step = WriteBackDialogStep.Confirm;
        }
    }

    /// <summary>Returns from confirmation to review. Writes nothing.</summary>
    public void Back()
    {
        if (IsConfirmStep)
        {
            Step = WriteBackDialogStep.Review;
        }
    }

    /// <summary>Writes the reviewed fields after the explicit step-2 confirmation.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfirmStep || !_owner.CanConfirmWriteBack)
        {
            return;
        }

        await _owner.ConfirmWriteBackAsync(cancellationToken).ConfigureAwait(true);
        Step = WriteBackDialogStep.Result;
    }

    /// <summary>Restores the original PDF from its backup.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task RestoreAsync(CancellationToken cancellationToken = default) =>
        CanRestore ? _owner.RestoreWriteBackAsync(cancellationToken) : Task.CompletedTask;

    /// <summary>Cancels: discards an unconfirmed backup token and closes. Never writes.</summary>
    public void Cancel()
    {
        HandleClosed();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called once the window has closed by any route (Cancel, Close, Esc or the title bar). An
    /// unconfirmed preparation is discarded; a completed write keeps its restore point.
    /// </summary>
    public void HandleClosed()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _owner.PropertyChanged -= OnOwnerPropertyChanged;
        if (!_owner.HasWrittenWriteBack)
        {
            _owner.CancelWriteBack();
        }
    }

    private string T(string key) => _localization[key];

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BookDetailViewModel.IsWritingWriteBack):
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(CanRestore));
                break;
            case nameof(BookDetailViewModel.CanConfirmWriteBack):
                OnPropertyChanged(nameof(CanConfirm));
                break;
            case nameof(BookDetailViewModel.CanRestoreWriteBack):
                OnPropertyChanged(nameof(CanRestore));
                break;
            case nameof(BookDetailViewModel.WriteBackBackupPath):
                OnPropertyChanged(nameof(BackupPath));
                break;
            case nameof(BookDetailViewModel.IsWriteBackError):
                OnPropertyChanged(nameof(IsError));
                break;
            case nameof(BookDetailViewModel.WriteBackStatusText):
                OnPropertyChanged(nameof(StatusText));
                break;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
