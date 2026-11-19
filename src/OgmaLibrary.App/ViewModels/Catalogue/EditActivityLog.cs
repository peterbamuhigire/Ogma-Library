using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Recent undoable catalogue edits, shown in the Activity Centre (Sept-23 Phase 10, task 10.8).
/// Undo follows the command history, which is last-in first-out, so only the newest edit that is
/// not yet undone offers Undo.
/// </summary>
public sealed class EditActivityLog : INotifyPropertyChanged
{
    private const int MaxEntries = 20;
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a new instance of the <see cref="EditActivityLog"/> class.</summary>
    /// <param name="localization">The localisation service.</param>
    public EditActivityLog(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        _localization.CultureChanged += (_, _) =>
        {
            foreach (EditActivityItem item in Items)
            {
                item.Relocalize();
            }

            OnPropertyChanged(nameof(Title));
        };
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Edits, newest first.</summary>
    public ObservableCollection<EditActivityItem> Items { get; } = [];

    /// <summary>Whether there is any edit to show.</summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>Section heading.</summary>
    public string Title => _localization["ActivityCentre.Edits.Title"];

    /// <summary>Undoes the newest edit; set by the owner of the command history.</summary>
    internal Func<CancellationToken, Task>? UndoLatest { get; set; }

    /// <summary>The newest edit that can still be undone.</summary>
    public EditActivityItem? Latest => Items.FirstOrDefault(item => !item.IsUndone && !item.IsUndoUnavailable);

    /// <summary>Records an applied bulk edit.</summary>
    /// <param name="field">The field changed.</param>
    /// <param name="bookCount">The number of books.</param>
    public void Record(BulkEditField field, int bookCount)
    {
        Items.Insert(0, new EditActivityItem(this, field, bookCount, _localization));
        while (Items.Count > MaxEntries)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        RaiseState();
    }

    /// <summary>Marks the newest edit as undone.</summary>
    public void MarkLatestUndone()
    {
        if (Latest is { } latest)
        {
            latest.IsUndone = true;
        }

        RaiseState();
    }

    /// <summary>
    /// Marks the newest edit as no longer undoable after a failed undo; the history has already
    /// dropped it, so Undo moves to the next older edit.
    /// </summary>
    public void MarkLatestUnavailable()
    {
        if (Latest is { } latest)
        {
            latest.IsUndoUnavailable = true;
        }

        RaiseState();
    }

    internal async Task UndoAsync(EditActivityItem item, CancellationToken cancellationToken)
    {
        if (ReferenceEquals(item, Latest) && UndoLatest is { } undo)
        {
            await undo(cancellationToken).ConfigureAwait(true);
        }
    }

    private void RaiseState()
    {
        foreach (EditActivityItem item in Items)
        {
            item.RaiseState();
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(Latest));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>One recent edit in the Activity Centre.</summary>
public sealed class EditActivityItem : INotifyPropertyChanged
{
    private readonly EditActivityLog _owner;
    private readonly BulkEditField _field;
    private readonly int _bookCount;
    private readonly ILocalizationService _localization;

    internal EditActivityItem(EditActivityLog owner, BulkEditField field, int bookCount, ILocalizationService localization)
    {
        _owner = owner;
        _field = field;
        _bookCount = bookCount;
        _localization = localization;
        Id = Guid.NewGuid().ToString("N");
        UndoCommand = new AsyncRelayCommand(token => _owner.UndoAsync(this, token), "activity.edit_undo", () => CanUndo);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>A stable identifier for automation.</summary>
    public string Id { get; }

    /// <summary>Whether the edit has been undone.</summary>
    public bool IsUndone { get; internal set; }

    /// <summary>Whether an undo of this edit failed, so it can no longer be undone.</summary>
    public bool IsUndoUnavailable { get; internal set; }

    /// <summary>Whether Undo is offered: this is the newest edit not yet undone.</summary>
    public bool CanUndo => ReferenceEquals(_owner.Latest, this);

    /// <summary>"Author changed on 3 books".</summary>
    public string Text => string.Format(
        CultureInfo.CurrentCulture,
        _localization[IsUndone ? "ActivityCentre.Edits.Undone" : "ActivityCentre.Edits.Entry"],
        _localization[$"BulkEdit.Field.{_field}"],
        _bookCount);

    /// <summary>Label of Undo.</summary>
    public string UndoText => _localization["ActivityCentre.Edits.Undo"];

    /// <summary>Undoes this edit when it is the newest.</summary>
    public ICommand UndoCommand { get; }

    /// <summary>Automation id of the Undo button.</summary>
    public string UndoAutomationId => $"ActivityCentre.Edits.Undo.{Id}";

    internal void RaiseState()
    {
        OnPropertyChanged(nameof(IsUndone));
        OnPropertyChanged(nameof(IsUndoUnavailable));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(Text));
        ((AsyncRelayCommand)UndoCommand).RaiseCanExecuteChanged();
    }

    internal void Relocalize()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(UndoText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
