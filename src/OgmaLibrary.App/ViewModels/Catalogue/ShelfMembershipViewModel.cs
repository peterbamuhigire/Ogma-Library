using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Adds books to and removes them from manual shelves (Sept-23 Phase 10, task 10.6). The card
/// menu, the selection toolbar and a drop onto a Collections row all come here. A smart shelf
/// (its books come from its saved filter) or a shelf deleted since its row was drawn rejects the
/// change with a localised notice and writes nothing. Failures go to the notification centre and
/// the redacted log, never as raw exception text.
/// </summary>
public sealed class ShelfMembershipViewModel : INotifyPropertyChanged
{
    private readonly ICatalogueReadModel _readModel;
    private readonly ICatalogueWriteService _writeService;
    private readonly ShelfSidebarViewModel _shelves;
    private readonly CatalogueViewModel _catalogue;
    private readonly IUserNotifier _notifier;
    private readonly ILogger _logger;
    private bool _isBusy;

    /// <summary>Initializes a new instance of <see cref="ShelfMembershipViewModel"/>.</summary>
    /// <param name="readModel">The catalogue read model, used to confirm the target shelf still exists.</param>
    /// <param name="writeService">The catalogue write service.</param>
    /// <param name="shelves">The shelf list (Collections); reloaded so counts stay current.</param>
    /// <param name="catalogue">The catalogue; reloaded so cards show their new shelves.</param>
    /// <param name="notifier">The notification centre.</param>
    /// <param name="logger">Optional logger.</param>
    public ShelfMembershipViewModel(
        ICatalogueReadModel readModel,
        ICatalogueWriteService writeService,
        ShelfSidebarViewModel shelves,
        CatalogueViewModel catalogue,
        IUserNotifier notifier,
        ILogger<ShelfMembershipViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(writeService);
        ArgumentNullException.ThrowIfNull(shelves);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(notifier);

        _readModel = readModel;
        _writeService = writeService;
        _shelves = shelves;
        _catalogue = catalogue;
        _notifier = notifier;
        _logger = logger ?? (ILogger)NullLogger.Instance;

        _shelves.Shelves.CollectionChanged += OnShelvesChanged;
        _catalogue.Filter.PropertyChanged += OnFilterChanged;
        SyncManualShelves();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The shelves a book can be added to: manual shelves only, in Collections order.</summary>
    public ObservableCollection<ShelfProjection> ManualShelves { get; } = [];

    /// <summary>Whether any manual shelf exists.</summary>
    public bool HasManualShelves => ManualShelves.Count > 0;

    /// <summary>The manual shelf the Library is filtered to, or <see langword="null"/>.</summary>
    public ShelfProjection? ActiveManualShelf =>
        _catalogue.Filter.SelectedShelfId is { } id ? ManualShelves.FirstOrDefault(shelf => shelf.ShelfId == id) : null;

    /// <summary>Whether a membership change is being written.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Adds <paramref name="bookIds"/> to the shelf <paramref name="shelfId"/>, then refreshes the
    /// cards and the shelf counts.
    /// </summary>
    /// <param name="shelfId">The target shelf.</param>
    /// <param name="bookIds">The books to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the books were added.</returns>
    public Task<bool> AddToShelfAsync(
        string shelfId,
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(shelfId, bookIds, add: true, cancellationToken);

    /// <summary>
    /// Handles books dropped on a Collections row. The same checks as the menu apply: a smart or
    /// deleted shelf rejects the drop with a notice and nothing is written.
    /// </summary>
    /// <param name="target">The shelf row the books were dropped on.</param>
    /// <param name="bookIds">The dragged books.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the books were added.</returns>
    public Task<bool> DropOnShelfAsync(
        ShelfProjection target,
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ChangeAsync(target.ShelfId, bookIds, add: true, cancellationToken);
    }

    /// <summary>Removes <paramref name="bookIds"/> from the manual shelf the Library is filtered to.</summary>
    /// <param name="bookIds">The books to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the books were removed.</returns>
    public Task<bool> RemoveFromActiveShelfAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default) =>
        ActiveManualShelf is { } shelf
            ? ChangeAsync(shelf.ShelfId, bookIds, add: false, cancellationToken)
            : Task.FromResult(false);

    private async Task<bool> ChangeAsync(
        string shelfId,
        IReadOnlyList<string> bookIds,
        bool add,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentNullException.ThrowIfNull(bookIds);
        if (bookIds.Count == 0 || IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            // The row may be stale: confirm the shelf still exists and is manual before writing.
            ShelfProjection? current = await FindShelfAsync(shelfId, cancellationToken).ConfigureAwait(true);
            if (current is null)
            {
                _notifier.Notify(new UserNotification(
                    "Catalogue.Shelves.Membership.Deleted",
                    UserNotificationSeverity.Warning));
                await _shelves.LoadAsync(cancellationToken).ConfigureAwait(true);
                return false;
            }

            if (current.IsSmart)
            {
                _notifier.Notify(new UserNotification(
                    "Catalogue.Shelves.Membership.Smart",
                    UserNotificationSeverity.Warning));
                return false;
            }

            foreach (string bookId in bookIds.Distinct(StringComparer.Ordinal))
            {
                if (add)
                {
                    await _writeService.AddBookToShelfAsync(shelfId, bookId, cancellationToken).ConfigureAwait(true);
                }
                else
                {
                    await _writeService.RemoveBookFromShelfAsync(shelfId, bookId, cancellationToken).ConfigureAwait(true);
                }
            }

            await _catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
            await _shelves.LoadAsync(cancellationToken).ConfigureAwait(true);
            _notifier.Notify(new UserNotification(
                add ? "Catalogue.Shelves.Membership.Added" : "Catalogue.Shelves.Membership.Removed",
                UserNotificationSeverity.Information));
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(ShelfMembershipViewModel), add ? "add" : "remove");
            _notifier.Notify(new UserNotification("Catalogue.Shelves.Membership.Failed"));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<ShelfProjection?> FindShelfAsync(string shelfId, CancellationToken cancellationToken)
    {
        await foreach (ShelfProjection shelf in _readModel.GetShelvesAsync(cancellationToken).ConfigureAwait(true))
        {
            if (string.Equals(shelf.ShelfId, shelfId, StringComparison.Ordinal))
            {
                return shelf;
            }
        }

        return null;
    }

    private void OnShelvesChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncManualShelves();

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(CatalogueFilterViewModel.SelectedShelfId))
        {
            OnPropertyChanged(nameof(ActiveManualShelf));
        }
    }

    private void SyncManualShelves()
    {
        // In place, so a sidebar refresh never empties the list for a moment (that would disable
        // the toolbar's Add to collection button and close its open menu).
        bool hadShelves = HasManualShelves;
        ShelfSidebarViewModel.SyncInPlace(
            ManualShelves,
            _shelves.Shelves.Where(shelf => !shelf.IsSmart).ToList());
        if (hadShelves != HasManualShelves)
        {
            OnPropertyChanged(nameof(HasManualShelves));
        }

        OnPropertyChanged(nameof(ActiveManualShelf));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
