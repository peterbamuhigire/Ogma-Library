using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Bookshelf3D.Messages;

namespace OgmaLibrary.App.ViewModels.Shelf3D;

/// <summary>View model for the WebView-hosted Three.js bookshelf.</summary>
public sealed class Bookshelf3DViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ICatalogueReadModel _catalogue;
    private readonly IWebViewBridge _bridge;
    private readonly IBookDetailNavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly string _toggleIconPath = IconCatalog.GetAvaresPath("ic_shelf3d_toggle") ?? string.Empty;
    private readonly string _shelfLayoutIconPath = IconCatalog.GetAvaresPath("ic_shelf3d_layout_shelf") ?? string.Empty;
    private readonly string _gridLayoutIconPath = IconCatalog.GetAvaresPath("ic_shelf3d_layout_grid3d") ?? string.Empty;
    private readonly string _unavailableIconPath = IconCatalog.GetAvaresPath("ic_shelf3d_unavailable") ?? string.Empty;
    private bool _isWebGl2Supported;
    private bool _isLoading;
    private string _currentLayout = "shelf";

    /// <summary>Initializes a new instance of <see cref="Bookshelf3DViewModel"/>.</summary>
    public Bookshelf3DViewModel(
        ICatalogueReadModel catalogue,
        IWebViewBridge bridge,
        IBookDetailNavigationService navigation,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _catalogue = catalogue;
        _bridge = bridge;
        _navigation = navigation;
        _localization = localization;
        _bridge.MessageReceived += OnBridgeMessageReceived;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Books currently represented in the 3D scene.</summary>
    public ObservableCollection<BookSceneItem> Books { get; } = [];

    /// <summary>Whether the WebView reported WebGL2 support.</summary>
    public bool IsWebGl2Supported
    {
        get => _isWebGl2Supported;
        private set
        {
            if (_isWebGl2Supported != value)
            {
                _isWebGl2Supported = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFallbackVisible));
            }
        }
    }

    /// <summary>Whether the accessible fallback should be visible.</summary>
    public bool IsFallbackVisible => !IsWebGl2Supported;

    /// <summary>Whether scene data is loading.</summary>
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

    /// <summary>Current 3D layout key.</summary>
    public string CurrentLayout
    {
        get => _currentLayout;
        private set
        {
            if (_currentLayout != value)
            {
                _currentLayout = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Localized title for the 3D bookshelf panel.</summary>
    public string Title => _localization["Shelf3D.Title"];

    /// <summary>Localized shelf-layout action label.</summary>
    public string ShelfLayoutLabel => _localization["Shelf3D.Layout.Shelf"];

    /// <summary>Localized grid-layout action label.</summary>
    public string GridLayoutLabel => _localization["Shelf3D.Layout.Grid3D"];

    /// <summary>Localized fallback message shown when WebGL2 is unavailable.</summary>
    public string FallbackMessage => _localization["Shelf3D.Fallback.Message"];

    /// <summary>Localized accessible label for the fallback list.</summary>
    public string FallbackListLabel => _localization["Shelf3D.Fallback.ListLabel"];

    /// <summary>Localized accessible label for the WebView host.</summary>
    public string WebViewHostLabel => _localization["Shelf3D.WebViewHost.Label"];

    /// <summary>Icon path for the 3D bookshelf identity.</summary>
    public string ToggleIconPath => _toggleIconPath;

    /// <summary>Icon path for the shelf layout action.</summary>
    public string ShelfLayoutIconPath => _shelfLayoutIconPath;

    /// <summary>Icon path for the 3D grid layout action.</summary>
    public string GridLayoutIconPath => _gridLayoutIconPath;

    /// <summary>Icon path for the unavailable/fallback state.</summary>
    public string UnavailableIconPath => _unavailableIconPath;

    /// <summary>Loads catalogue books and sends a full scene to JavaScript.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            Books.Clear();
            await foreach (BookSummaryProjection summary in _catalogue.GetBookSummariesAsync(
                new CatalogueFilter(Status: 0, MaxResults: 500),
                cancellationToken).ConfigureAwait(false))
            {
                Books.Add(ToSceneItem(summary));
            }

            try
            {
                await _bridge.PostMessageAsync(
                    new SetSceneMessage(Books.ToArray(), DefaultCamera()),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // No platform adapter is a capability failure, not a catalogue
                // failure. Keep the local list usable as the accessible fallback.
                IsWebGl2Supported = false;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Sends a layout change to JavaScript.</summary>
    public async Task SetLayoutAsync(string layout, CancellationToken cancellationToken = default)
    {
        if (layout is not ("shelf" or "grid3d"))
        {
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "Layout must be 'shelf' or 'grid3d'.");
        }

        CurrentLayout = layout;
        try
        {
            await _bridge.PostMessageAsync(new SetLayoutMessage(layout), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            IsWebGl2Supported = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bridge.MessageReceived -= OnBridgeMessageReceived;
        _localization.CultureChanged -= OnCultureChanged;
    }

    private static BookSceneItem ToSceneItem(BookSummaryProjection summary)
    {
        string title = LimitLabel(string.IsNullOrWhiteSpace(summary.Title) ? "Untitled" : summary.Title, 120);
        string author = LimitLabel(summary.Authors.FirstOrDefault(author => !string.IsNullOrWhiteSpace(author)) ?? "Unknown author", 80);
        string encodedBookId = Uri.EscapeDataString(summary.BookId);
        string? coverUri = string.IsNullOrWhiteSpace(summary.CoverRelativePath)
            ? null
            : $"ogma://assets/covers/{Uri.EscapeDataString(Path.GetFileName(summary.CoverRelativePath))}";

        return new BookSceneItem(
            summary.BookId,
            title,
            author,
            $"ogma://assets/spines/{encodedBookId}.png",
            coverUri);
    }

    private static string LimitLabel(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..(maximumLength - 1)].TrimEnd() + "…";

    private static CameraState DefaultCamera() => new(0, 0.45, 1.4, 0, 0, 0, 45);

    private void OnBridgeMessageReceived(object? sender, InboundMessage message)
    {
        switch (message)
        {
            case BookClickedMessage clicked:
                _ = _navigation.OpenDetailAsync(clicked.BookId, CancellationToken.None);
                break;
            case BookDoubleClickedMessage doubleClicked:
                _ = _navigation.OpenDetailAsync(doubleClicked.BookId, CancellationToken.None);
                break;
            case WebGl2StatusMessage status:
                IsWebGl2Supported = status.Supported;
                break;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ShelfLayoutLabel));
        OnPropertyChanged(nameof(GridLayoutLabel));
        OnPropertyChanged(nameof(FallbackMessage));
        OnPropertyChanged(nameof(FallbackListLabel));
        OnPropertyChanged(nameof(WebViewHostLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
