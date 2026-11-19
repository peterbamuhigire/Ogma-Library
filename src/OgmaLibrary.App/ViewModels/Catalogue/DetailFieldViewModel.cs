using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>The interaction state of one inline-editable inspector field.</summary>
public enum DetailFieldState
{
    /// <summary>The value is shown read-only.</summary>
    Display,

    /// <summary>The value is in an editor.</summary>
    Edit,

    /// <summary>The edit is being written to the catalogue.</summary>
    Saving,

    /// <summary>The edit was rejected or could not be saved; the editor stays open.</summary>
    Error,
}

/// <summary>
/// One inline-editable metadata field in the book inspector's Details section (Sept-23 Phase 11,
/// task 11.4): Title, Authors, Year, ISBN, DOI or Publisher. Enter or leaving the editor commits,
/// Esc cancels. Validation uses <see cref="MetadataFieldEditRules"/>, the same rules the write
/// service enforces. Provenance (source, confidence, "Edited by you") is computed only when the
/// popover first opens. All members are used on the UI thread; the owning
/// <see cref="BookDetailViewModel"/> marshals save results back before touching state.
/// </summary>
public sealed class DetailFieldViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localization;
    private readonly Func<DetailFieldViewModel, string?, CancellationToken, Task> _save;
    private readonly Func<bool> _canEdit;
    private DetailFieldState _state = DetailFieldState.Display;
    private string? _value;
    private string _editText = string.Empty;
    private string? _errorKey;
    private bool _isProvenanceLoaded;
    private bool _isEditedByYou;
    private string? _provenanceText;
    private BookDetailProjection? _book;

    /// <summary>Initializes a new instance of the <see cref="DetailFieldViewModel"/> class.</summary>
    /// <param name="fieldName">One of <see cref="EditableMetadataFields.All"/>.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="save">Writes a validated, normalised value; owned by the inspector.</param>
    /// <param name="canEdit">Whether editing is currently possible.</param>
    internal DetailFieldViewModel(
        string fieldName,
        ILocalizationService localization,
        Func<DetailFieldViewModel, string?, CancellationToken, Task> save,
        Func<bool> canEdit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(canEdit);
        FieldName = fieldName;
        _localization = localization;
        _save = save;
        _canEdit = canEdit;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The catalogue metadata field name.</summary>
    public string FieldName { get; }

    /// <summary>Stable automation identifier of the display button.</summary>
    public string AutomationId => "Detail.Field." + FieldName;

    /// <summary>Stable automation identifier of the editor.</summary>
    public string EditorAutomationId => AutomationId + ".Editor";

    /// <summary>Stable automation identifier of the provenance button.</summary>
    public string ProvenanceAutomationId => AutomationId + ".Provenance";

    /// <summary>Stable automation identifier of the inline error.</summary>
    public string ErrorAutomationId => AutomationId + ".Error";

    /// <summary>Localised field label.</summary>
    public string LabelText => _localization["Catalogue.BookDetail.Edit.Label." + FieldName];

    /// <summary>Localised accessible name of the display button.</summary>
    public string EditAccessibleText => string.Format(
        _localization.CurrentCulture, _localization["Catalogue.BookDetail.Edit.EditFormat"], LabelText);

    /// <summary>Localised accessible name of the provenance button.</summary>
    public string ProvenanceAccessibleText => string.Format(
        _localization.CurrentCulture, _localization["Catalogue.BookDetail.Edit.ProvenanceFormat"], LabelText);

    /// <summary>The current state.</summary>
    public DetailFieldState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(IsDisplaying));
                OnPropertyChanged(nameof(IsSaving));
                OnPropertyChanged(nameof(IsEditorEnabled));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>True while the editor is shown (editing, saving or showing an error).</summary>
    public bool IsEditing => _state != DetailFieldState.Display;

    /// <summary>True while the read-only value is shown.</summary>
    public bool IsDisplaying => _state == DetailFieldState.Display;

    /// <summary>True while the edit is being written.</summary>
    public bool IsSaving => _state == DetailFieldState.Saving;

    /// <summary>True when the editor accepts input.</summary>
    public bool IsEditorEnabled => _state != DetailFieldState.Saving;

    /// <summary>True when an inline error is shown.</summary>
    public bool HasError => _state == DetailFieldState.Error && _errorKey is not null;

    /// <summary>The stored value, or <see langword="null"/> when the field is not set.</summary>
    public string? Value
    {
        get => _value;
        private set
        {
            if (!string.Equals(_value, value, StringComparison.Ordinal))
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>The value shown read-only, or the localised "Not set".</summary>
    public string DisplayText => _value ?? _localization["Catalogue.BookDetail.Edit.Empty"];

    /// <summary>True when the field has no value.</summary>
    public bool IsEmpty => _value is null;

    /// <summary>The editor text.</summary>
    public string EditText
    {
        get => _editText;
        set
        {
            value ??= string.Empty;
            if (!string.Equals(_editText, value, StringComparison.Ordinal))
            {
                _editText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The localised inline error, or <see langword="null"/>.</summary>
    public string? ErrorText => _errorKey is null ? null : _localization[_errorKey];

    /// <summary>True once the provenance popover has been opened for this book.</summary>
    public bool IsProvenanceLoaded => _isProvenanceLoaded;

    /// <summary>True when the effective value was entered by the user.</summary>
    public bool IsEditedByYou => _isEditedByYou;

    /// <summary>Localised provenance summary; empty until <see cref="LoadProvenance"/> runs.</summary>
    public string ProvenanceText => _provenanceText ?? string.Empty;

    /// <summary>Opens the editor with the current value.</summary>
    public void BeginEdit()
    {
        if (_state != DetailFieldState.Display || !_canEdit())
        {
            return;
        }

        EditText = _value ?? string.Empty;
        SetError(null);
        State = DetailFieldState.Edit;
    }

    /// <summary>Discards the editor text and returns to display.</summary>
    public void CancelEdit()
    {
        if (_state == DetailFieldState.Saving)
        {
            return;
        }

        EditText = _value ?? string.Empty;
        SetError(null);
        State = DetailFieldState.Display;
    }

    /// <summary>
    /// Validates the editor text and saves it when it changed. An invalid value shows a localised
    /// inline error and writes nothing.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_state is not (DetailFieldState.Edit or DetailFieldState.Error))
        {
            return;
        }

        MetadataFieldEditError error = MetadataFieldEditRules.TryNormalize(FieldName, _editText, out string? normalized);
        if (error != MetadataFieldEditError.None)
        {
            SetError(ErrorKeyFor(error));
            State = DetailFieldState.Error;
            return;
        }

        if (string.Equals(normalized, _value, StringComparison.Ordinal))
        {
            CancelEdit();
            return;
        }

        SetError(null);
        State = DetailFieldState.Saving;
        await _save(this, normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Computes the provenance summary for the popover (first open, or on request).</summary>
    public void LoadProvenance()
    {
        MetadataFieldProjection? source = EffectiveRow(_book);
        _isEditedByYou = source?.IsOverridden == true;
        if (_isEditedByYou)
        {
            _provenanceText = _localization["Catalogue.BookDetail.Edit.Provenance.EditedByYou"];
        }
        else
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(source?.Source))
            {
                parts.Add(string.Format(
                    _localization.CurrentCulture,
                    _localization["Catalogue.BookDetail.Edit.Provenance.SourceFormat"],
                    source.Source.Trim()));
            }

            if (source?.Confidence is double confidence)
            {
                parts.Add(string.Format(
                    _localization.CurrentCulture,
                    _localization["Catalogue.BookDetail.Edit.Provenance.ConfidenceFormat"],
                    Math.Clamp(confidence, 0.0, 1.0).ToString("P0", _localization.CurrentCulture)));
            }

            _provenanceText = parts.Count == 0
                ? _localization["Catalogue.BookDetail.Edit.Provenance.None"]
                : string.Join(Environment.NewLine, parts);
        }

        _isProvenanceLoaded = true;
        OnPropertyChanged(nameof(IsProvenanceLoaded));
        OnPropertyChanged(nameof(IsEditedByYou));
        OnPropertyChanged(nameof(ProvenanceText));
    }

    /// <summary>Applies a new book projection. A different book resets edit and provenance state.</summary>
    internal void Refresh(BookDetailProjection? book, bool changedBook)
    {
        _book = book;
        Value = ReadValue(book);
        if (changedBook)
        {
            SetError(null);
            State = DetailFieldState.Display;
            _isProvenanceLoaded = false;
            _isEditedByYou = false;
            _provenanceText = null;
            OnPropertyChanged(nameof(IsProvenanceLoaded));
            OnPropertyChanged(nameof(IsEditedByYou));
            OnPropertyChanged(nameof(ProvenanceText));
        }
        else if (_isProvenanceLoaded)
        {
            LoadProvenance();
        }

        if (_state == DetailFieldState.Display)
        {
            EditText = _value ?? string.Empty;
        }
    }

    /// <summary>Marks a save as written; the projection has already been refreshed.</summary>
    internal void CompleteSave()
    {
        SetError(null);
        State = DetailFieldState.Display;
        EditText = _value ?? string.Empty;
    }

    /// <summary>Shows the localised save failure and keeps the user's text in the editor.</summary>
    internal void FailSave()
    {
        SetError("Catalogue.BookDetail.Edit.Error.SaveFailed");
        State = DetailFieldState.Error;
    }

    /// <summary>Returns a cancelled save to the editor without an error.</summary>
    internal void AbandonSave()
    {
        if (_state == DetailFieldState.Saving)
        {
            State = DetailFieldState.Edit;
        }
    }

    /// <summary>Raises change notifications for every localised member.</summary>
    internal void OnCultureChanged()
    {
        OnPropertyChanged(nameof(LabelText));
        OnPropertyChanged(nameof(EditAccessibleText));
        OnPropertyChanged(nameof(ProvenanceAccessibleText));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ErrorText));
        if (_isProvenanceLoaded)
        {
            LoadProvenance();
        }
    }

    private static string ErrorKeyFor(MetadataFieldEditError error) => error switch
    {
        MetadataFieldEditError.Author => "Catalogue.BookDetail.Edit.Error.Author",
        MetadataFieldEditError.Year => "Catalogue.BookDetail.Edit.Error.Year",
        MetadataFieldEditError.Isbn => "Catalogue.BookDetail.Edit.Error.ISBN",
        MetadataFieldEditError.Doi => "Catalogue.BookDetail.Edit.Error.DOI",
        _ => "Catalogue.BookDetail.Edit.Error.Text",
    };

    private string? ReadValue(BookDetailProjection? book)
    {
        if (book is null)
        {
            return null;
        }

        string? value = FieldName switch
        {
            EditableMetadataFields.Title => book.Title,
            EditableMetadataFields.Author => BulkEditFieldRules.FormatAuthors(book.Authors),
            EditableMetadataFields.Year => book.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EditableMetadataFields.Isbn => book.Isbn,
            EditableMetadataFields.Doi => book.Doi,
            _ => EffectiveRow(book)?.Value,
        };
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // The row the read model would show: user-entered first, then the most confident provider.
    private MetadataFieldProjection? EffectiveRow(BookDetailProjection? book) =>
        book?.MetadataFields
            .Where(f => string.Equals(f.FieldName, FieldName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.IsOverridden)
            .ThenByDescending(f => f.Confidence ?? 0)
            .FirstOrDefault();

    private void SetError(string? key)
    {
        if (!string.Equals(_errorKey, key, StringComparison.Ordinal))
        {
            _errorKey = key;
            OnPropertyChanged(nameof(ErrorText));
        }

        OnPropertyChanged(nameof(HasError));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
