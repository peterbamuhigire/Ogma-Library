using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private bool _isWebGl2Supported = true;
    private bool _isLoading;
    private string _currentLayout = "shelf";

    /// <summary>Initializes a new instance of <see cref="Bookshelf3DViewModel"/>.</summary>
    public Bookshelf3DViewModel(
        ICatalogueReadModel catalogue,
        IWebViewBridge bridge,
        IBookDetailNavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(navigation);

        _catalogue = catalogue;
        _bridge = bridge;
        _navigation = navigation;
        _bridge.MessageReceived += OnBridgeMessageReceived;
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

            await _bridge.PostMessageAsync(
                new SetSceneMessage(Books.ToArray(), DefaultCamera()),
                cancellationToken).ConfigureAwait(false);
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
        await _bridge.PostMessageAsync(new SetLayoutMessage(layout), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _bridge.MessageReceived -= OnBridgeMessageReceived;

    private static BookSceneItem ToSceneItem(BookSummaryProjection summary)
    {
        string title = string.IsNullOrWhiteSpace(summary.Title) ? "Untitled" : summary.Title;
        string author = summary.Authors.FirstOrDefault(author => !string.IsNullOrWhiteSpace(author)) ?? "Unknown author";
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
