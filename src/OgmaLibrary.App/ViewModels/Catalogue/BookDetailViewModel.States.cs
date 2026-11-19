using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Ocr;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Explicit inspector states and the hash-verified per-book relink (Sept-23 Phase 11 slice 6,
/// task 11.7): loading, missing file, password-protected, processing, failed processing and
/// no metadata each map to one template and one primary action.
/// </summary>
public sealed partial class BookDetailViewModel
{
    private const string RelinkKeyPrefix = "Catalogue.BookDetail.Relink.";

    private IBookRelinkService? _relink;
    private bool _fileMissingOnDisk;
    private bool _isRelinking;
    private string? _relinkStatusKey;

    /// <summary>
    /// Asks the user for the moved PDF; set by the view to its storage-provider file picker.
    /// Returns the chosen local path, or <see langword="null"/> when the user cancelled.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? LocateFilePicker { get; set; }

    /// <summary>The single state the inspector is in.</summary>
    public InspectorState State => IsLoading
        ? InspectorState.Loading
        : _book switch
        {
            null => InspectorState.None,
            _ when IsFileMissing => InspectorState.MissingFile,
            { IsPasswordProtected: true } => InspectorState.PasswordProtected,
            _ when IsQueueingOcr || _book.TextStatus == BookTextStatus.OcrInProgress => InspectorState.Processing,
            _ when _book.TextStatus == BookTextStatus.OcrFailed || !string.IsNullOrWhiteSpace(_book.OcrFailureCode) =>
                InspectorState.ProcessingFailed,
            _ when HasNoMetadata => InspectorState.NoMetadata,
            _ => InspectorState.Ready,
        };

    /// <summary>True while the skeleton replaces the inspector content.</summary>
    public bool IsLoadingState => State == InspectorState.Loading;

    /// <summary>True when the header and sections are shown (not loading).</summary>
    public bool IsContentVisible => !IsLoadingState;

    /// <summary>True in the missing-file state.</summary>
    public bool IsMissingFileState => State == InspectorState.MissingFile;

    /// <summary>True in the password-protected state.</summary>
    public bool IsPasswordState => State == InspectorState.PasswordProtected;

    /// <summary>True while text recognition is queued or running.</summary>
    public bool IsProcessingState => State == InspectorState.Processing;

    /// <summary>True when the latest text recognition failed.</summary>
    public bool IsProcessingFailedState => State == InspectorState.ProcessingFailed;

    /// <summary>True while a relink request is running.</summary>
    public bool IsRelinking
    {
        get => _isRelinking;
        private set
        {
            if (_isRelinking != value)
            {
                _isRelinking = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanLocateFile));
                OnPropertyChanged(nameof(CanExecutePrimaryAction));
            }
        }
    }

    /// <summary>Whether <c>Locate file…</c> can run now.</summary>
    public bool CanLocateFile => _book is not null && !IsRelinking;

    /// <summary>Whether the failed-processing <c>Retry</c> re-queues text recognition.</summary>
    public bool CanRetryProcessing => CanRunOcr;

    /// <summary>Localised outcome of the latest relink, or <see langword="null"/>.</summary>
    public string? RelinkStatusText => _relinkStatusKey is null ? null : _localization[_relinkStatusKey];

    /// <summary>True when <see cref="RelinkStatusText"/> is shown.</summary>
    public bool HasRelinkStatus => _relinkStatusKey is not null;

    /// <summary>Localised accessible name of the loading skeleton.</summary>
    public string LoadingStateText => _localization["Catalogue.BookDetail.State.Loading"];

    /// <summary>Localised missing-file heading.</summary>
    public string MissingStateTitleText => _localization["Catalogue.BookDetail.State.Missing.Title"];

    /// <summary>Localised missing-file explanation.</summary>
    public string MissingStateBodyText => _localization["Catalogue.BookDetail.State.Missing.Body"];

    /// <summary>Localised <c>Locate file…</c> label.</summary>
    public string LocateFileText => _localization["Catalogue.BookDetail.Primary.LocateFile"];

    /// <summary>Localised title of the file picker.</summary>
    public string LocateFilePickerTitle => _localization["Catalogue.BookDetail.Relink.PickerTitle"];

    /// <summary>Localised name of the PDF file-type filter in the picker.</summary>
    public string PdfFilesFilterText => _localization["MainWindow.PdfPicker.PdfFiles"];

    /// <summary>Localised password-protected heading.</summary>
    public string PasswordStateTitleText => _localization["Catalogue.BookDetail.State.Password.Title"];

    /// <summary>Localised reason <c>Unlock</c> is disabled (unlocking arrives in Phase 12, K125).</summary>
    public string PasswordStateBodyText => _localization["Catalogue.BookDetail.State.Password.Body"];

    /// <summary>Localised processing heading.</summary>
    public string ProcessingStateTitleText => _localization["Catalogue.BookDetail.State.Processing.Title"];

    /// <summary>Localised processing explanation.</summary>
    public string ProcessingStateBodyText => _localization["Catalogue.BookDetail.State.Processing.Body"];

    /// <summary>Localised failed-processing heading.</summary>
    public string FailedStateTitleText => _localization["Catalogue.BookDetail.State.Failed.Title"];

    /// <summary>Localised <c>Retry</c> label.</summary>
    public string RetryProcessingText => _localization["Catalogue.BookDetail.State.Retry"];

    private bool IsFileMissing => _book is not null && (!_book.IsAvailable || _fileMissingOnDisk);

    /// <summary>
    /// Asks for the moved PDF through <see cref="LocateFilePicker"/> and relinks the book when
    /// the file is inside an enabled library folder and its SHA-256 matches.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task LocateFileAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || IsRelinking)
        {
            return;
        }

        if (_relink is null || LocateFilePicker is null)
        {
            SetOpenError("Catalogue.BookDetail.Open.LocateUnavailable");
            return;
        }

        string? path = await LocateFilePicker(cancellationToken).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await RelinkFileAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Relinks the loaded book to <paramref name="candidatePath"/> and refreshes the inspector.</summary>
    /// <param name="candidatePath">The local path the user chose. Untrusted; never displayed or logged.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RelinkFileAsync(string candidatePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        if (_book is null || _relink is null || IsRelinking)
        {
            return;
        }

        string bookId = _book.BookId;
        IsRelinking = true;
        SetOpenError(null);
        SetRelinkStatus(null);
        BookRelinkResult result;
        try
        {
            result = await _relink.RelinkAsync(bookId, candidatePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, ViewModelName, "book.relink");
            result = BookRelinkResult.Failed;
        }
        finally
        {
            UpdateOnUiThread(() => IsRelinking = false);
        }

        if (result == BookRelinkResult.Relinked)
        {
            UpdateOnUiThread(() =>
            {
                if (string.Equals(_book?.BookId, bookId, StringComparison.Ordinal))
                {
                    _fileMissingOnDisk = false;
                }
            });
            await RefreshLoadedBookAsync(cancellationToken).ConfigureAwait(false);
        }

        UpdateOnUiThread(() =>
        {
            if (string.Equals(_book?.BookId, bookId, StringComparison.Ordinal))
            {
                SetRelinkStatus(RelinkKeyPrefix + result);
            }
        });
    }

    /// <summary>Re-queues text recognition after a failure (the existing OCR queue contract).</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task RetryProcessingAsync(CancellationToken cancellationToken = default) =>
        RunOcrAsync(cancellationToken);

    private void SetRelinkStatus(string? key)
    {
        if (!string.Equals(_relinkStatusKey, key, StringComparison.Ordinal))
        {
            _relinkStatusKey = key;
            OnPropertyChanged(nameof(RelinkStatusText));
            OnPropertyChanged(nameof(HasRelinkStatus));
        }
    }

    // Deleted while open (11.7): a failed locate during Read marks the file missing; a later
    // successful locate (Read or refresh) clears it.
    private void SetFileMissingOnDisk(string bookId, bool missing)
    {
        if (!string.Equals(_book?.BookId, bookId, StringComparison.Ordinal) || _fileMissingOnDisk == missing)
        {
            return;
        }

        _fileMissingOnDisk = missing;
        NotifyAvailabilityChanged();
    }

    private async Task ReevaluateFileOnDiskAsync(string bookId, CancellationToken cancellationToken)
    {
        // Only a book already found missing is re-checked, so a refresh never touches the disk
        // (or a classroom Host) for the common case.
        if (!_fileMissingOnDisk || _fileLocator is null)
        {
            return;
        }

        string? path = await _fileLocator.LocateAsync(bookId, cancellationToken).ConfigureAwait(false);
        UpdateOnUiThread(() => SetFileMissingOnDisk(bookId, path is null));
    }

    private void NotifyAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanOpenReader));
        OnPropertyChanged(nameof(FileAvailabilityText));
        OnPropertyChanged(nameof(PrimaryAction));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(CanExecutePrimaryAction));
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsLoadingState));
        OnPropertyChanged(nameof(IsContentVisible));
        OnPropertyChanged(nameof(IsMissingFileState));
        OnPropertyChanged(nameof(IsPasswordState));
        OnPropertyChanged(nameof(IsProcessingState));
        OnPropertyChanged(nameof(IsProcessingFailedState));
        OnPropertyChanged(nameof(CanLocateFile));
        OnPropertyChanged(nameof(CanRetryProcessing));
    }

    private void OnStatesBookChanged(bool changedBook)
    {
        if (changedBook)
        {
            _fileMissingOnDisk = false;
            SetRelinkStatus(null);
        }

        NotifyAvailabilityChanged();
    }

    private void OnStatesCultureChanged()
    {
        OnPropertyChanged(nameof(RelinkStatusText));
        OnPropertyChanged(nameof(LoadingStateText));
        OnPropertyChanged(nameof(MissingStateTitleText));
        OnPropertyChanged(nameof(MissingStateBodyText));
        OnPropertyChanged(nameof(LocateFileText));
        OnPropertyChanged(nameof(LocateFilePickerTitle));
        OnPropertyChanged(nameof(PdfFilesFilterText));
        OnPropertyChanged(nameof(PasswordStateTitleText));
        OnPropertyChanged(nameof(PasswordStateBodyText));
        OnPropertyChanged(nameof(ProcessingStateTitleText));
        OnPropertyChanged(nameof(ProcessingStateBodyText));
        OnPropertyChanged(nameof(FailedStateTitleText));
        OnPropertyChanged(nameof(RetryProcessingText));
    }
}
