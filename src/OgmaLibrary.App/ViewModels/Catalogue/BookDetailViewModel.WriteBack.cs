namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Write-back restore point and dialog factory for Sept-23 Phase 11 slice 5 (task 11.6). A
/// successful write keeps its backup token so <c>Restore original</c> stays reachable under
/// Activity after the consent dialog closes.
/// </summary>
public sealed partial class BookDetailViewModel
{
    private bool _writeBackWritten;
    private bool _isWriteBackError;

    /// <summary>True when the last write-back status describes a failure.</summary>
    public bool IsWriteBackError
    {
        get => _isWriteBackError;
        private set
        {
            if (_isWriteBackError != value)
            {
                _isWriteBackError = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True when metadata was written to the PDF and its original can still be restored.</summary>
    public bool HasWriteBackRestorePoint => _writeBackToken is not null && _writeBackWritten;

    /// <summary>True when the PDF has been written in this session and still holds its restore point.</summary>
    public bool HasWrittenWriteBack => _writeBackWritten;

    /// <summary>Absolute path of the retained backup copy, or null when no backup is held.</summary>
    public string? WriteBackBackupPath => _writeBackToken?.BackupAbsolutePath;

    /// <summary>Localised label of the restore action (dialog and Activity card).</summary>
    public string RestoreOriginalText => _localization["Catalogue.BookDetail.WriteBack.RestoreOriginal"];

    /// <summary>Localised explanation on the Activity restore card.</summary>
    public string WriteBackRestoreCardText => _localization["Catalogue.BookDetail.WriteBack.RestoreCard"];

    /// <summary>Creates the two-step consent dialog for the prepared write-back.</summary>
    /// <returns>A dialog view model bound to this inspector's write-back state.</returns>
    public WriteBackDialogViewModel CreateWriteBackDialog() => new(this, _localization);

    private void NotifyWriteBackStateChanged()
    {
        OnPropertyChanged(nameof(HasPreparedWriteBack));
        OnPropertyChanged(nameof(HasWriteBackDiff));
        OnPropertyChanged(nameof(CanPrepareWriteBack));
        OnPropertyChanged(nameof(CanConfirmWriteBack));
        OnPropertyChanged(nameof(CanRestoreWriteBack));
        OnPropertyChanged(nameof(HasWriteBackRestorePoint));
        OnPropertyChanged(nameof(HasWrittenWriteBack));
        OnPropertyChanged(nameof(WriteBackBackupPath));
    }

    private void OnWriteBackCultureChanged()
    {
        OnPropertyChanged(nameof(RestoreOriginalText));
        OnPropertyChanged(nameof(WriteBackRestoreCardText));
    }
}
