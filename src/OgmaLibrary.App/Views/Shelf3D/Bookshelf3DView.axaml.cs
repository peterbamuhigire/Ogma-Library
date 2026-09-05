using Avalonia;
using Avalonia.Controls;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.App.ViewModels.Shelf3D;

namespace OgmaLibrary.App.Views.Shelf3D;

/// <summary>View for the WebView-hosted 3D bookshelf and accessible fallback.</summary>
public partial class Bookshelf3DView : UserControl
{
    private IWebViewHostAdapter? _hostAdapter;

    /// <summary>Initializes a new instance of <see cref="Bookshelf3DView"/>.</summary>
    public Bookshelf3DView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is not Bookshelf3DViewModel viewModel)
        {
            return;
        }

        _hostAdapter = new NativeWebViewHostAdapter(WebViewHost);
        if (_hostAdapter is NativeWebViewHostAdapter nativeHost)
        {
            nativeHost.HostUnavailable += OnNativeHostUnavailable;
        }

        await viewModel.InitializeNativeHostAsync(_hostAdapter).ConfigureAwait(true);
        if (viewModel.Books.Count == 0)
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private async void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_hostAdapter is NativeWebViewHostAdapter nativeHost)
        {
            nativeHost.HostUnavailable -= OnNativeHostUnavailable;
        }

        if (_hostAdapter is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }

        _hostAdapter = null;
    }

    private void OnNativeHostUnavailable(object? sender, EventArgs e)
    {
        if (DataContext is Bookshelf3DViewModel viewModel)
        {
            viewModel.ReportNativeHostUnavailable();
        }
    }

    private async void ShelfLayout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is Bookshelf3DViewModel viewModel)
        {
            await viewModel.SetLayoutAsync("shelf").ConfigureAwait(false);
        }
    }

    private async void GridLayout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is Bookshelf3DViewModel viewModel)
        {
            await viewModel.SetLayoutAsync("grid3d").ConfigureAwait(false);
        }
    }
}
