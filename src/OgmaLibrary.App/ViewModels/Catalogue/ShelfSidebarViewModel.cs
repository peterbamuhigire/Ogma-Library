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
    private string _newShelfName = string.Empty;
    private string? _statusText;

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

    /// <summary>Localized shelves heading.</summary>
    public string ShelvesLabel => _localization["Catalogue.Shelves.Title"];

    /// <summary>Localized create-collection action label.</summary>
    public string CreateShelfLabel => _localization["Catalogue.Shelves.Create"];

    /// <summary>Localized delete-collection action label.</summary>
    public string DeleteShelfLabel => _localization["Catalogue.Shelves.Delete"];

    /// <summary>Localized rename-collection action label.</summary>
    public string RenameShelfLabel => _localization["Catalogue.Shelves.Rename"];

    /// <summary>Localized collection-name prompt.</summary>
    public string NewShelfWatermark => _localization["Catalogue.Shelves.NameWatermark"];

    /// <summary>New collection name entered in the sidebar.</summary>
    public string NewShelfName
    {
        get => _newShelfName;
        set
        {
            if (_newShelfName != value)
            {
                _newShelfName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCreateShelf));
                OnPropertyChanged(nameof(CanRenameSelectedShelf));
            }
        }
    }

    /// <summary>True when a valid collection name can be submitted.</summary>
    public bool CanCreateShelf => !string.IsNullOrWhiteSpace(NewShelfName) && !IsLoading;

    /// <summary>True when a collection is selected for deletion.</summary>
    public bool CanDeleteSelectedShelf => SelectedShelf is not null && !IsLoading;

    /// <summary>True when a selected collection can be renamed.</summary>
    public bool CanRenameSelectedShelf => SelectedShelf is not null && CanCreateShelf;

    /// <summary>Localized result of the latest collection mutation.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>True when a collection mutation status should be displayed.</summary>
    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

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
                OnPropertyChanged(nameof(CanDeleteSelectedShelf));
                OnPropertyChanged(nameof(CanRenameSelectedShelf));
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
                OnPropertyChanged(nameof(CanCreateShelf));
                OnPropertyChanged(nameof(CanDeleteSelectedShelf));
                OnPropertyChanged(nameof(CanRenameSelectedShelf));
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

    /// <summary>Creates the named user collection from the sidebar editor.</summary>
    public async Task CreateNewShelfAsync(CancellationToken cancellationToken = default)
    {
        if (!CanCreateShelf)
        {
            StatusText = _localization["Catalogue.Shelves.NameRequired"];
            return;
        }

        try
        {
            await CreateShelfAsync(NewShelfName.Trim(), cancellationToken).ConfigureAwait(false);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                NewShelfName = string.Empty;
                StatusText = _localization["Catalogue.Shelves.Created"];
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Catalogue.Shelves.FailedFormat"],
                ex.Message);
        }
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

    /// <summary>Deletes the selected user collection and clears its filter.</summary>
    public async Task DeleteSelectedShelfAsync(CancellationToken cancellationToken = default)
    {
        ShelfProjection? shelf = SelectedShelf;
        if (shelf is null)
        {
            StatusText = _localization["Catalogue.Shelves.NoneSelected"];
            return;
        }

        try
        {
            await DeleteShelfAsync(shelf.ShelfId, cancellationToken).ConfigureAwait(false);
            StatusText = _localization["Catalogue.Shelves.Deleted"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Catalogue.Shelves.FailedFormat"],
                ex.Message);
        }
    }

    /// <summary>Renames the selected user collection from the sidebar editor.</summary>
    public async Task RenameSelectedShelfAsync(CancellationToken cancellationToken = default)
    {
        ShelfProjection? shelf = SelectedShelf;
        if (shelf is null)
        {
            StatusText = _localization["Catalogue.Shelves.NoneSelected"];
            return;
        }

        if (!CanRenameSelectedShelf)
        {
            StatusText = _localization["Catalogue.Shelves.NameRequired"];
            return;
        }

        try
        {
            await RenameShelfAsync(shelf.ShelfId, NewShelfName.Trim(), cancellationToken)
                .ConfigureAwait(false);
            StatusText = _localization["Catalogue.Shelves.Renamed"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Catalogue.Shelves.FailedFormat"],
                ex.Message);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
