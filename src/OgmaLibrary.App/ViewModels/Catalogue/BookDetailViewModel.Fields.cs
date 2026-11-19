using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Inline metadata editing for Sept-23 Phase 11 slice 3 (task 11.4): the editable Details fields,
/// saving through an undoable command and the panel's Undo affordance.
/// </summary>
public sealed partial class BookDetailViewModel
{
    private const string ViewModelName = nameof(BookDetailViewModel);
    private IReadOnlyList<DetailFieldViewModel>? _editableFields;
    private string? _fieldsBookId;
    private MetadataFieldEditUndoableCommand? _lastFieldEdit;
    private DetailFieldViewModel? _lastEditedField;
    private bool _isUndoingFieldEdit;
    private string? _fieldEditStatusKey;
    private string? _fieldEditStatusLabelKey;

    /// <summary>The inline-editable metadata fields, in inspector order.</summary>
    public IReadOnlyList<DetailFieldViewModel> EditableFields => _editableFields ??= CreateEditableFields();

    /// <summary>True when inline edits can be saved (a book is loaded and the write boundary exists).</summary>
    public bool CanEditFields => _book is not null && _catalogueWriteService is not null;

    /// <summary>Bibliographic rows other than the inline-editable fields (language, categories, description).</summary>
    public IReadOnlyList<string> OtherBiblioFieldDisplayRows =>
        BiblioFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value) && !IsEditableField(f.FieldName))
            .Select(FormatField)
            .ToList();

    /// <summary>True when the last inline edit on this book can be undone.</summary>
    public bool CanUndoFieldEdit => _lastFieldEdit?.CanUndo == true && !_isUndoingFieldEdit;

    /// <summary>Localised Undo button label.</summary>
    public string UndoFieldEditText => _localization["Catalogue.BookDetail.Edit.Undo"];

    /// <summary>Localised accessible name of the Undo button, naming the edited field.</summary>
    public string UndoFieldEditAccessibleText => _lastEditedField is null
        ? UndoFieldEditText
        : string.Format(
            _localization.CurrentCulture,
            _localization["Catalogue.BookDetail.Edit.UndoFormat"],
            _lastEditedField.LabelText);

    /// <summary>Localised result of the last inline edit or undo, or <see langword="null"/>.</summary>
    public string? FieldEditStatusText
    {
        get
        {
            if (_fieldEditStatusKey is null)
            {
                return null;
            }

            string text = _localization[_fieldEditStatusKey];
            return _fieldEditStatusLabelKey is null
                ? text
                : string.Format(_localization.CurrentCulture, text, _localization[_fieldEditStatusLabelKey]);
        }
    }

    /// <summary>True when <see cref="FieldEditStatusText"/> has content.</summary>
    public bool HasFieldEditStatus => _fieldEditStatusKey is not null;

    /// <summary>
    /// Undoes the last inline edit on the loaded book. The edit is undone directly rather than by
    /// popping the shared command history, which may hold a later bulk edit (D-63); the command
    /// itself guarantees the edit is restored at most once.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task UndoFieldEditAsync(CancellationToken cancellationToken = default)
    {
        MetadataFieldEditUndoableCommand? command = _lastFieldEdit;
        if (command is null || !command.CanUndo || _isUndoingFieldEdit)
        {
            return;
        }

        string bookId = command.BookId;
        SetUndoingFieldEdit(true);
        try
        {
            await command.UndoAsync(cancellationToken).ConfigureAwait(false);
            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                if (string.Equals(_book?.BookId, bookId, StringComparison.Ordinal))
                {
                    Book = detail ?? _book;
                }

                _lastFieldEdit = null;
                _lastEditedField = null;
                SetFieldEditStatus("Catalogue.BookDetail.Edit.Undone", null);
                SetUndoingFieldEdit(false);
            });
        }
        catch (OperationCanceledException)
        {
            UpdateOnUiThread(() => SetUndoingFieldEdit(false));
        }
        catch (Exception ex)
        {
            AppLog.ViewModelOperationFailed(_logger, ex, ViewModelName, "metadata.edit.undo");
            UpdateOnUiThread(() =>
            {
                SetFieldEditStatus("Catalogue.BookDetail.Edit.Error.UndoFailed", null);
                SetUndoingFieldEdit(false);
            });
        }
    }

    private DetailFieldViewModel[] CreateEditableFields()
    {
        DetailFieldViewModel[] fields = EditableMetadataFields.All
            .Select(name => new DetailFieldViewModel(name, _localization, SaveFieldAsync, () => CanEditFields))
            .ToArray();
        foreach (DetailFieldViewModel field in fields)
        {
            field.Refresh(_book, changedBook: true);
        }

        _fieldsBookId = _book?.BookId;
        return fields;
    }

    // Runs one inline edit through the command history (when bound), then reloads the projection
    // and applies the result on the UI thread. Failures are logged through the redacting logger
    // and shown as localised text only.
    private async Task SaveFieldAsync(DetailFieldViewModel field, string? value, CancellationToken cancellationToken)
    {
        BookDetailProjection? book = _book;
        if (book is null || _catalogueWriteService is null)
        {
            UpdateOnUiThread(field.FailSave);
            return;
        }

        string bookId = book.BookId;
        var command = new MetadataFieldEditUndoableCommand(_catalogueWriteService, bookId, field.FieldName, value);
        try
        {
            if (_commandHistory is not null)
            {
                await _commandHistory.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                if (!string.Equals(_book?.BookId, bookId, StringComparison.Ordinal))
                {
                    return;
                }

                Book = detail ?? _book;
                field.CompleteSave();
                _lastFieldEdit = command;
                _lastEditedField = field;
                SetFieldEditStatus("Catalogue.BookDetail.Edit.SavedFormat", "Catalogue.BookDetail.Edit.Label." + field.FieldName);
                OnPropertyChanged(nameof(CanUndoFieldEdit));
                OnPropertyChanged(nameof(UndoFieldEditAccessibleText));
            });
        }
        catch (OperationCanceledException)
        {
            UpdateOnUiThread(field.AbandonSave);
        }
        catch (Exception ex)
        {
            AppLog.ViewModelOperationFailed(_logger, ex, ViewModelName, "metadata.edit.save");
            UpdateOnUiThread(field.FailSave);
        }
    }

    private void OnFieldsBookChanged()
    {
        bool changedBook = !string.Equals(_fieldsBookId, _book?.BookId, StringComparison.Ordinal);
        _fieldsBookId = _book?.BookId;
        if (changedBook)
        {
            _lastFieldEdit = null;
            _lastEditedField = null;
            SetFieldEditStatus(null, null);
            OnPropertyChanged(nameof(CanUndoFieldEdit));
            OnPropertyChanged(nameof(UndoFieldEditAccessibleText));
        }

        if (_editableFields is not null)
        {
            foreach (DetailFieldViewModel field in _editableFields)
            {
                field.Refresh(_book, changedBook);
            }
        }

        OnPropertyChanged(nameof(CanEditFields));
        OnPropertyChanged(nameof(OtherBiblioFieldDisplayRows));
    }

    private void OnFieldsCultureChanged()
    {
        if (_editableFields is not null)
        {
            foreach (DetailFieldViewModel field in _editableFields)
            {
                field.OnCultureChanged();
            }
        }

        OnPropertyChanged(nameof(UndoFieldEditText));
        OnPropertyChanged(nameof(UndoFieldEditAccessibleText));
        OnPropertyChanged(nameof(FieldEditStatusText));
        OnPropertyChanged(nameof(OtherBiblioFieldDisplayRows));
    }

    private void SetUndoingFieldEdit(bool value)
    {
        _isUndoingFieldEdit = value;
        OnPropertyChanged(nameof(CanUndoFieldEdit));
    }

    private void SetFieldEditStatus(string? key, string? labelKey)
    {
        _fieldEditStatusKey = key;
        _fieldEditStatusLabelKey = labelKey;
        OnPropertyChanged(nameof(FieldEditStatusText));
        OnPropertyChanged(nameof(HasFieldEditStatus));
    }

    private static bool IsEditableField(string fieldName) =>
        EditableMetadataFields.All.Any(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase));
}
