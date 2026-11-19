using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The bulk-edit form (Sept-23 Phase 10, task 10.8, CAT-005): a field picker, a value, and a
/// before-and-after preview built by <see cref="BulkEditPlanner"/>. It writes nothing; the caller
/// applies <see cref="BuildCommand"/> through the command history.
/// </summary>
public sealed class BulkEditViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<BookSummaryProjection> _books;
    private readonly ILocalizationService _localization;
    private BulkEditFieldOption _selectedField;
    private string _value = string.Empty;
    private BulkEditPlan _plan = new([], IsValid: false);
    private bool _hasInvalidValue;

    /// <summary>Initializes a new instance of the <see cref="BulkEditViewModel"/> class.</summary>
    /// <param name="books">The selected books.</param>
    /// <param name="localization">The localisation service.</param>
    public BulkEditViewModel(IReadOnlyList<BookSummaryProjection> books, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(localization);
        _books = books;
        _localization = localization;
        Fields =
        [
            new BulkEditFieldOption(BulkEditField.Author, localization),
            new BulkEditFieldOption(BulkEditField.Year, localization),
            new BulkEditFieldOption(BulkEditField.Rating, localization),
        ];
        _selectedField = Fields[0];
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The fields the form can change.</summary>
    public IReadOnlyList<BulkEditFieldOption> Fields { get; }

    /// <summary>The field being changed.</summary>
    public BulkEditFieldOption SelectedField
    {
        get => _selectedField;
        set
        {
            if (value is null || ReferenceEquals(_selectedField, value))
            {
                return;
            }

            _selectedField = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HintText));
            Recompute();
        }
    }

    /// <summary>The new value as typed.</summary>
    public string Value
    {
        get => _value;
        set
        {
            string next = value ?? string.Empty;
            if (string.Equals(_value, next, StringComparison.Ordinal))
            {
                return;
            }

            _value = next;
            OnPropertyChanged();
            Recompute();
        }
    }

    /// <summary>One row per selected book.</summary>
    public ObservableCollection<BulkEditPreviewItem> PreviewRows { get; } = [];

    /// <summary>The number of books edited.</summary>
    public int BookCount => _books.Count;

    /// <summary>Whether the value is valid and changes at least one book.</summary>
    public bool CanApply => _plan.CanApply;

    /// <summary>Whether a typed value is not acceptable for the field.</summary>
    public bool HasValidationError => _hasInvalidValue;

    /// <summary>Whether the form was applied (set by <see cref="Apply"/>).</summary>
    public bool IsApplied { get; private set; }

    /// <summary>The window title and heading.</summary>
    public string Title => string.Format(CultureInfo.CurrentCulture, _localization["BulkEdit.Title"], BookCount);

    /// <summary>One sentence about what the form does.</summary>
    public string IntroText => _localization["BulkEdit.Intro"];

    /// <summary>Label of the field picker.</summary>
    public string FieldLabel => _localization["BulkEdit.Field"];

    /// <summary>Label of the value box.</summary>
    public string ValueLabel => _localization["BulkEdit.Value"];

    /// <summary>Persistent help for the selected field.</summary>
    public string HintText => _localization[$"BulkEdit.Hint.{_selectedField.Field}"];

    /// <summary>The error for an unacceptable value, or empty.</summary>
    public string ValidationText => _hasInvalidValue ? _localization[$"BulkEdit.Invalid.{_selectedField.Field}"] : string.Empty;

    /// <summary>Heading of the preview.</summary>
    public string PreviewLabel => _localization["BulkEdit.Preview"];

    /// <summary>Column headings.</summary>
    public string BookColumnText => _localization["BulkEdit.Preview.Book"];

    /// <summary>Column heading of the current value.</summary>
    public string BeforeColumnText => _localization["BulkEdit.Preview.Before"];

    /// <summary>Column heading of the new value.</summary>
    public string AfterColumnText => _localization["BulkEdit.Preview.After"];

    /// <summary>"2 of 3 books will change", or a prompt to type a value.</summary>
    public string SummaryText => _plan.Rows.Count == 0 || _hasInvalidValue
        ? _localization["BulkEdit.Preview.Empty"]
        : string.Format(
            CultureInfo.CurrentCulture,
            _localization["BulkEdit.Preview.Summary"],
            _plan.Rows.Where(row => row.IsChange).Select(row => row.BookId).Distinct(StringComparer.Ordinal).Count(),
            BookCount);

    /// <summary>Label of Apply.</summary>
    public string ApplyText => _localization["BulkEdit.Apply"];

    /// <summary>Label of Cancel.</summary>
    public string CancelText => _localization["BulkEdit.Cancel"];

    /// <summary>The field changed, for the activity entry.</summary>
    public BulkEditField Field => _selectedField.Field;

    /// <summary>The edit to apply, or <see langword="null"/> when the form cannot apply.</summary>
    /// <returns>The command.</returns>
    public BulkEditCommand? BuildCommand() => CanApply ? CreateCommand() : null;

    /// <summary>Marks the form as applied when it can apply.</summary>
    /// <returns><see langword="true"/> when the form was applied.</returns>
    public bool Apply()
    {
        IsApplied = CanApply;
        return IsApplied;
    }

    private BulkEditCommand? CreateCommand()
    {
        string trimmed = _value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        string[] ids = _books.Select(book => book.BookId).ToArray();
        switch (_selectedField.Field)
        {
            case BulkEditField.Author:
                return new BulkEditCommand(ids, [], [], null, null, null, null, AuthorToSet: _value);
            case BulkEditField.Year:
                return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int year)
                    ? new BulkEditCommand(ids, [], [], null, null, null, null, YearToSet: year)
                    : null;
            default:
                return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int rating)
                    ? new BulkEditCommand(ids, [], [], null, null, null, rating)
                    : null;
        }
    }

    private void Recompute()
    {
        BulkEditCommand? command = CreateCommand();
        _plan = command is null ? new BulkEditPlan([], IsValid: false) : BulkEditPlanner.Plan(command, _books);
        _hasInvalidValue = _value.Trim().Length > 0 && !_plan.IsValid;

        PreviewRows.Clear();
        foreach (BulkEditPreviewRow row in _plan.Rows)
        {
            PreviewRows.Add(new BulkEditPreviewItem(row, _localization));
        }

        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>A field choice in the bulk-edit picker.</summary>
public sealed class BulkEditFieldOption
{
    /// <summary>Initializes a new instance of the <see cref="BulkEditFieldOption"/> class.</summary>
    /// <param name="field">The field.</param>
    /// <param name="localization">The localisation service.</param>
    public BulkEditFieldOption(BulkEditField field, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        Field = field;
        Label = localization[$"BulkEdit.Field.{field}"];
    }

    /// <summary>The field.</summary>
    public BulkEditField Field { get; }

    /// <summary>The localised label.</summary>
    public string Label { get; }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>One preview row: title, current value and new value.</summary>
public sealed class BulkEditPreviewItem
{
    /// <summary>Initializes a new instance of the <see cref="BulkEditPreviewItem"/> class.</summary>
    /// <param name="row">The planned row.</param>
    /// <param name="localization">The localisation service.</param>
    public BulkEditPreviewItem(BulkEditPreviewRow row, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(localization);
        Row = row;
        Title = row.Title ?? string.Empty;
        BeforeText = row.Before ?? localization["BulkEdit.NotSet"];
        AfterText = row.After ?? localization["BulkEdit.NotSet"];
        AccessibleName = string.Format(
            CultureInfo.CurrentCulture,
            localization["BulkEdit.Preview.RowName"],
            Title,
            BeforeText,
            AfterText);
    }

    /// <summary>The planned row.</summary>
    public BulkEditPreviewRow Row { get; }

    /// <summary>The book title.</summary>
    public string Title { get; }

    /// <summary>The current value.</summary>
    public string BeforeText { get; }

    /// <summary>The value after applying.</summary>
    public string AfterText { get; }

    /// <summary>Whether the value changes.</summary>
    public bool IsChange => Row.IsChange;

    /// <summary>Screen-reader name: "Title: now, becomes after".</summary>
    public string AccessibleName { get; }

    /// <summary>Stable automation id.</summary>
    public string AutomationId => $"BulkEdit.Row.{Row.BookId}";
}
