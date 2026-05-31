using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// View model for the shelf sidebar (FR-CAT-003). Loads shelf projections,
/// exposes commands to create / rename / delete shelves, and integrates with
/// <see cref="CatalogueFilterViewModel"/> to filter the catalogue to the selected shelf.
/// </summary>
public sealed class ShelfSidebarViewModel : INotifyPropertyChanged
{
    private readonly ICatalogueReadModel _readModel;
    private readonly ICatalogueWriteService _writeService;
    private readonly ILocalizationService _localization;
    private readonly CatalogueFilterViewModel _filter;

    private ShelfProjection? _selectedShelf;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of <see cref="ShelfSidebarViewModel"/>.
    /// </summary>
    /// <param name="readModel">The catalogue read model.</param>
    /// <param name="writeService">The catalogue write service.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="filter">The shared filter view model to update on shelf selection.</param>
    public ShelfSidebarViewModel(
        ICatalogueReadModel readModel,
        ICatalogueWriteService writeService,
        ILocalizationService localization,
        CatalogueFilterViewModel filter)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(writeService);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(filter);

        _readModel = readModel;
        _writeService = writeService;
        _localization = localization;
        _filter = filter;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The list of all shelves (virtual + smart).</summary>
    public ObservableCollection<ShelfProjection> Shelves { get; } = [];

    /// <summary>The currently selected shelf; <see langword="null"/> means "All books".</summary>
    public ShelfProjection? SelectedShelf
    {
        get => _selectedShelf;
        set
        {
            if (!ReferenceEquals(_selectedShelf, value))
            {
                _selectedShelf = value;
                OnPropertyChanged();
                _filter.SelectedShelfId = value?.ShelfId;
            }
        }
    }

    /// <summary>True while the shelf list is loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Loads or reloads all shelves from the read model.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            var shelves = new List<ShelfProjection>();
            await foreach (var shelf in _readModel.GetShelvesAsync(cancellationToken).ConfigureAwait(false))
            {
                shelves.Add(shelf);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Shelves.Clear();
                foreach (var shelf in shelves)
                {
                    Shelves.Add(shelf);
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Creates a new virtual shelf with the given name.</summary>
    /// <param name="name">The display name for the new shelf.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task CreateShelfAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _writeService.CreateShelfAsync(name, isSmart: false, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Renames the specified shelf.</summary>
    /// <param name="shelfId">The shelf to rename.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        await _writeService.RenameShelfAsync(shelfId, newName, cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes the specified shelf.</summary>
    /// <param name="shelfId">The shelf to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        await _writeService.DeleteShelfAsync(shelfId, cancellationToken).ConfigureAwait(false);

        // If the deleted shelf was selected, clear the selection.
        if (_selectedShelf?.ShelfId == shelfId)
        {
            SelectedShelf = null;
        }

        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
