using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Commands;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The catalogue selection toolbar (Sept-23 Phase 10, task 10.7). It shows when two or more
/// books are selected, or when one is selected and a context action applies (the Library is
/// filtered to a manual shelf, so "Remove from shelf" is available, or the card has copies or
/// editions to compare or split). Actions: Add to shelf, Edit… (bulk edit with preview and undo,
/// task 10.8), Compare, Merge into one work and Split (task 10.9), Mark as read, Enrich (only with
/// a metadata provider enabled) and Clear selection.
/// </summary>
public sealed class CatalogueSelectionViewModel : INotifyPropertyChanged
{
    private readonly CatalogueViewModel _catalogue;
    private readonly ILocalizationService _localization;
    private readonly IUserNotifier _notifier;
    private readonly IBookCurationService? _curation;
    private readonly IBookMetadataEnrichmentService? _enrichment;
    private readonly ICapabilityState? _capabilities;
    private readonly ICatalogueWriteService? _writeService;
    private readonly ICommandHistory? _history;
    private readonly ILogger _logger;

    // Bulk edits applied from this toolbar, oldest first, mirroring the LIFO command history so a
    // toast or Activity Centre entry can undo only its own edit.
    private const int MaxUndoableEdits = 20;
    private readonly List<BulkEditUndoableCommand> _applied = [];
    private bool _isBusy;

    /// <summary>Initializes a new instance of <see cref="CatalogueSelectionViewModel"/>.</summary>
    /// <param name="catalogue">The catalogue whose <see cref="CatalogueViewModel.SelectedBooks"/> drive the toolbar.</param>
    /// <param name="membership">Shelf membership actions.</param>
    /// <param name="localization">The localisation service.</param>
    /// <param name="notifier">The notification centre.</param>
    /// <param name="curation">Personal reading state, for Mark as read; absent hides nothing but disables it.</param>
    /// <param name="enrichment">Provider enrichment, for Enrich.</param>
    /// <param name="capabilities">Capability state; Enrich shows only when metadata providers are enabled.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="writeService">Catalogue writes, for bulk edit.</param>
    /// <param name="history">The undo history; bulk edit is offered only with both this and <paramref name="writeService"/>.</param>
    /// <param name="editLog">Recent edits shown in the Activity Centre.</param>
    /// <param name="identity">Compare, Merge into one work and Split (task 10.9).</param>
    public CatalogueSelectionViewModel(
        CatalogueViewModel catalogue,
        ShelfMembershipViewModel membership,
        ILocalizationService localization,
        IUserNotifier notifier,
        IBookCurationService? curation = null,
        IBookMetadataEnrichmentService? enrichment = null,
        ICapabilityState? capabilities = null,
        ILogger<CatalogueSelectionViewModel>? logger = null,
        ICatalogueWriteService? writeService = null,
        ICommandHistory? history = null,
        EditActivityLog? editLog = null,
        IdentityGroupingViewModel? identity = null)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notifier);

        _catalogue = catalogue;
        Membership = membership;
        _localization = localization;
        _notifier = notifier;
        _curation = curation;
        _enrichment = enrichment;
        _capabilities = capabilities;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _writeService = writeService;
        _history = history;
        EditLog = editLog;
        if (EditLog is not null)
        {
            EditLog.UndoLatest = UndoLastEditAsync;
        }

        Identity = identity;
        if (Identity is not null)
        {
            Identity.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsToolbarVisible));
        }

        _catalogue.SelectedBooks.CollectionChanged += OnSelectionChanged;
        _catalogue.SelectionRestored += (_, _) => RaiseState();
        Membership.PropertyChanged += (_, _) => RaiseState();
        Membership.ManualShelves.CollectionChanged += (_, _) => RaiseState();
        _localization.CultureChanged += (_, _) => RaiseText();
        if (_capabilities is not null)
        {
            _capabilities.Changed += (_, _) => RaiseState();
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Shelf membership actions shared with the card menu and Collections drop.</summary>
    public ShelfMembershipViewModel Membership { get; }

    /// <summary>The number of selected books.</summary>
    public int Count => _catalogue.SelectedBooks.Count;

    /// <summary>Whether the toolbar is shown.</summary>
    public bool IsToolbarVisible => Count >= 2 ||
        (Count == 1 && (CanRemoveFromShelf || Identity?.CanCompare == true || Identity?.CanSplit == true));

    /// <summary>Compare, Merge into one work and Split, or null when grouping is unavailable.</summary>
    public IdentityGroupingViewModel? Identity { get; }

    /// <summary>Whether a membership or bulk action is running.</summary>
    public bool IsBusy => _isBusy || Membership.IsBusy;

    /// <summary>Whether the selection can be added to a manual shelf.</summary>
    public bool CanAddToShelf => Count > 0 && Membership.HasManualShelves && !IsBusy;

    /// <summary>Whether the selection can be removed from the manual shelf the Library is filtered to.</summary>
    public bool CanRemoveFromShelf => Count > 0 && Membership.ActiveManualShelf is not null && !IsBusy;

    /// <summary>Whether bulk edit (Edit…) can open for the selection.</summary>
    public bool CanEdit => Count > 0 && _writeService is not null && _history is not null && !IsBusy;

    /// <summary>Recent edits with Undo, shared with the Activity Centre.</summary>
    public EditActivityLog? EditLog { get; }

    /// <summary>Whether the selection can be marked as read.</summary>
    public bool CanMarkAsRead => Count > 0 && _curation is not null && !IsBusy;

    /// <summary>Whether Enrich is offered: an online metadata provider is enabled.</summary>
    public bool IsEnrichVisible => _enrichment is not null && _capabilities?.AreMetadataProvidersEnabled == true;

    /// <summary>Whether Enrich can run now.</summary>
    public bool CanEnrich => IsEnrichVisible && Count > 0 && !IsBusy;

    /// <summary>"3 selected".</summary>
    public string CountText => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Catalogue.Selection.CountFormat"],
        Count);

    /// <summary>Accessible name of the toolbar.</summary>
    public string ToolbarName => _localization["Catalogue.Selection.Toolbar"];

    /// <summary>Label of Add to shelf.</summary>
    public string AddToShelfText => _localization["Catalogue.Selection.AddToShelf"];

    /// <summary>Label of Remove from shelf.</summary>
    public string RemoveFromShelfText => _localization["Catalogue.Selection.RemoveFromShelf"];

    /// <summary>Label of Edit….</summary>
    public string EditText => _localization["Catalogue.Selection.Edit"];

    /// <summary>Label of Mark as read.</summary>
    public string MarkReadText => _localization["Catalogue.Selection.MarkRead"];

    /// <summary>Label of Enrich.</summary>
    public string EnrichText => _localization["Catalogue.Selection.Enrich"];

    /// <summary>Label of Clear selection.</summary>
    public string ClearText => _localization["Catalogue.Selection.Clear"];

    /// <summary>Shown in the Add to shelf menu when there is no manual shelf yet.</summary>
    public string NoShelvesText => _localization["Catalogue.Selection.NoShelves"];

    /// <summary>Adds the selection to <paramref name="shelfId"/>; the selection is kept.</summary>
    /// <param name="shelfId">The target manual shelf.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task AddSelectionToShelfAsync(string shelfId, CancellationToken cancellationToken = default)
    {
        await Membership.AddToShelfAsync(shelfId, SelectedIds(), cancellationToken).ConfigureAwait(true);
        RaiseState();
    }

    /// <summary>Removes the selection from the manual shelf the Library is filtered to.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RemoveSelectionFromShelfAsync(CancellationToken cancellationToken = default)
    {
        await Membership.RemoveFromActiveShelfAsync(SelectedIds(), cancellationToken).ConfigureAwait(true);
        RaiseState();
    }

    /// <summary>Marks every selected book as finished.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task MarkSelectionAsReadAsync(CancellationToken cancellationToken = default) =>
        _curation is null || !CanMarkAsRead
            ? Task.CompletedTask
            : RunAsync(
                "mark_read",
                (bookId, token) => _curation.UpdateReadingStateAsync(bookId, ReadingStatus.Finished, cancellationToken: token),
                "Catalogue.Selection.MarkedRead",
                "Catalogue.Selection.MarkReadFailed",
                cancellationToken);

    /// <summary>Runs provider enrichment for every selected book.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task EnrichSelectionAsync(CancellationToken cancellationToken = default) =>
        _enrichment is null || !CanEnrich
            ? Task.CompletedTask
            : RunAsync(
                "enrich",
                async (bookId, token) =>
                {
                    (bool success, _) = await _enrichment.EnrichAsync(bookId, absoluteFilePath: null, token).ConfigureAwait(true);
                    if (!success)
                    {
                        throw new InvalidOperationException("Enrichment did not succeed.");
                    }
                },
                "Catalogue.Selection.EnrichDone",
                "Catalogue.Selection.EnrichFailed",
                cancellationToken);

    /// <summary>Creates the bulk-edit form for the current selection, or <see langword="null"/> when Edit… is unavailable.</summary>
    /// <returns>The form view model.</returns>
    public BulkEditViewModel? CreateBulkEdit() =>
        CanEdit ? new BulkEditViewModel(_catalogue.SelectedBooks.ToArray(), _localization) : null;

    /// <summary>
    /// Applies an accepted bulk-edit form through the command history, reloads the catalogue and
    /// offers Undo in a notification and in the Activity Centre. A form that was cancelled or
    /// cannot apply writes nothing.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ApplyBulkEditAsync(BulkEditViewModel form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (!form.IsApplied || form.BuildCommand() is not { } command || _writeService is null || _history is null)
        {
            return;
        }

        _isBusy = true;
        RaiseState();
        try
        {
            var undoable = new BulkEditUndoableCommand(_writeService, command);
            await _history.ExecuteAsync(undoable, cancellationToken).ConfigureAwait(true);
            _applied.Add(undoable);
            if (_applied.Count > MaxUndoableEdits)
            {
                _applied.RemoveAt(0);
            }

            EditLog?.Record(form.Field, command.BookIds.Count);
            // The toast undoes only its own edit: once that edit is undone (for example from the
            // Activity Centre), a stale toast must not undo an older edit (LIFO history).
            _notifier.Notify(new UserNotification(
                "Catalogue.BulkEdit.Applied",
                UserNotificationSeverity.Information,
                UserNotificationActionKind.Undo,
                token => UndoEditAsync(undoable, token)));
            await _catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(CatalogueSelectionViewModel), "bulk_edit");
            _notifier.Notify(new UserNotification("Catalogue.BulkEdit.Failed"));
        }
        finally
        {
            _isBusy = false;
            RaiseState();
        }
    }

    /// <summary>Undoes the most recent bulk edit and reloads the catalogue.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task UndoLastEditAsync(CancellationToken cancellationToken = default) =>
        _applied.Count == 0 ? Task.CompletedTask : UndoEditAsync(_applied[^1], cancellationToken);

    private async Task UndoEditAsync(BulkEditUndoableCommand expected, CancellationToken cancellationToken)
    {
        if (_history is null || !_history.CanUndo || _applied.Count == 0 || !ReferenceEquals(_applied[^1], expected))
        {
            return;
        }

        _isBusy = true;
        RaiseState();
        bool undone = false;
        try
        {
            // The history pops the entry before undoing, so the edit leaves both stacks either way.
            _applied.RemoveAt(_applied.Count - 1);
            await _history.UndoAsync(cancellationToken).ConfigureAwait(true);
            undone = true;
            EditLog?.MarkLatestUndone();
            _notifier.Notify(new UserNotification("Catalogue.BulkEdit.Undone", UserNotificationSeverity.Information));
            await _catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(CatalogueSelectionViewModel), "bulk_edit_undo");
            if (!undone)
            {
                EditLog?.MarkLatestUnavailable();
                _notifier.Notify(new UserNotification("Catalogue.BulkEdit.UndoFailed"));
            }
        }
        finally
        {
            _isBusy = false;
            RaiseState();
        }
    }

    /// <summary>Clears the selection; the toolbar hides.</summary>
    public void ClearSelection() => _catalogue.SelectedBooks.Clear();

    private async Task RunAsync(
        string operation,
        Func<string, CancellationToken, Task> perBook,
        string doneKey,
        string failedKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ids = SelectedIds();
        _isBusy = true;
        RaiseState();
        try
        {
            foreach (string bookId in ids)
            {
                await perBook(bookId, cancellationToken).ConfigureAwait(true);
            }

            await _catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
            _notifier.Notify(new UserNotification(doneKey, UserNotificationSeverity.Information));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(CatalogueSelectionViewModel), operation);
            _notifier.Notify(new UserNotification(failedKey));
        }
        finally
        {
            _isBusy = false;
            RaiseState();
        }
    }

    private string[] SelectedIds() =>
        _catalogue.SelectedBooks.Select(book => book.BookId).Distinct(StringComparer.Ordinal).ToArray();

    // A background refresh briefly drops replaced cards from the selection; reacting would hide
    // the toolbar and close an open Add to collection menu, so wait for SelectionRestored.
    private void OnSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_catalogue.IsRestoringSelection)
        {
            RaiseState();
        }
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsToolbarVisible));
        OnPropertyChanged(nameof(CanAddToShelf));
        OnPropertyChanged(nameof(CanRemoveFromShelf));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanMarkAsRead));
        OnPropertyChanged(nameof(IsEnrichVisible));
        OnPropertyChanged(nameof(CanEnrich));
    }

    private void RaiseText()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ToolbarName));
        OnPropertyChanged(nameof(AddToShelfText));
        OnPropertyChanged(nameof(RemoveFromShelfText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(MarkReadText));
        OnPropertyChanged(nameof(EnrichText));
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(NoShelvesText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
