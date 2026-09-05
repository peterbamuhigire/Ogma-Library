using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.Bookshelf3D.Bridge;

namespace OgmaLibrary.App.Views.Shelf3D;

/// <summary>View for the WebView-hosted 3D bookshelf and accessible fallback.</summary>
public partial class Bookshelf3DView : UserControl
{
    private IWebViewHostAdapter? _hostAdapter;
    private readonly List<IDisposable> _visibilitySubscriptions = [];
    private CancellationTokenSource? _hostCancellation;

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

        SubscribeToAncestorVisibility();
        await InitializeNativeHostIfVisibleAsync(viewModel).ConfigureAwait(true);
    }

    private async void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        foreach (IDisposable subscription in _visibilitySubscriptions)
        {
            subscription.Dispose();
        }

        _visibilitySubscriptions.Clear();
        CancellationTokenSource? hostCancellation = _hostCancellation;
        _hostCancellation = null;
        hostCancellation?.Cancel();
        hostCancellation?.Dispose();

        if (_hostAdapter is NativeWebViewHostAdapter nativeHost)
        {
            nativeHost.HostUnavailable -= OnNativeHostUnavailable;
        }

        if (_hostAdapter is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }

        _hostAdapter = null;
        WebViewHost.Content = null;
    }

    private void SubscribeToAncestorVisibility()
    {
        for (Visual? current = this.GetVisualParent(); current is not null; current = current.GetVisualParent())
        {
            if (current is Control control)
            {
                _visibilitySubscriptions.Add(control.GetObservable(IsVisibleProperty).Subscribe(isVisible =>
                {
                    _ = InitializeNativeHostIfVisibleAsync(DataContext as Bookshelf3DViewModel);
                }));
            }
        }
    }

    private async Task InitializeNativeHostIfVisibleAsync(Bookshelf3DViewModel? viewModel)
    {
        if (viewModel is null || !viewModel.HasNativeHostCoordinator ||
            !IsEffectivelyVisible || _hostAdapter is not null)
        {
            return;
        }

        var hostCancellation = new CancellationTokenSource();
        _hostCancellation = hostCancellation;
        var nativeWebView = new Avalonia.Controls.NativeWebView
        {
            Focusable = true,
            IsTabStop = true,
        };
        WebViewHost.Content = nativeWebView;
        var nativeHost = new NativeWebViewHostAdapter(nativeWebView);
        nativeHost.HostUnavailable += OnNativeHostUnavailable;
        _hostAdapter = nativeHost;

        try
        {
            await viewModel.InitializeNativeHostAsync(_hostAdapter, hostCancellation.Token)
                .ConfigureAwait(true);
            if (viewModel.Books.Count == 0 && !hostCancellation.IsCancellationRequested)
            {
                await viewModel.LoadAsync(hostCancellation.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (hostCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_hostCancellation, hostCancellation))
            {
                _hostCancellation = null;
                hostCancellation.Dispose();
            }
        }
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
