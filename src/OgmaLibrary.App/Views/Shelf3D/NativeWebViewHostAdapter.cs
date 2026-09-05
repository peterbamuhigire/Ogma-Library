using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;

namespace OgmaLibrary.App.Views.Shelf3D;

/// <summary>
/// Adapts Avalonia's native WebView control to the shared shelf bridge.
///
/// The public cross-platform WebView API does not currently synthesize custom
/// scheme response bodies. Authorized <c>ogma://</c> resources are consequently
/// served through a loopback-only, random-token HTTP endpoint while the existing
/// scheme handler remains the authority for path validation and file access.
/// </summary>
public sealed class NativeWebViewHostAdapter : IWebViewHostAdapter, IAsyncDisposable
{
    private readonly NativeWebView _webView;
    private LoopbackAssetServer? _assetServer;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes an adapter around an attached Avalonia native WebView.</summary>
    public NativeWebViewHostAdapter(NativeWebView webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _webView.WebMessageReceived += OnWebMessageReceived;
        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.AdapterDestroyed += OnAdapterDestroyed;
    }

    /// <inheritdoc />
    public event EventHandler<string>? RawMessageReceived;

    /// <summary>Raised when the native host can no longer render the shelf.</summary>
    public event EventHandler? HostUnavailable;

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PostJsonAsync(string json, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ThrowIfReady();
        cancellationToken.ThrowIfCancellationRequested();
        string browserJson = _assetServer?.RewriteAssetUris(json) ?? json;
        string script = $"window.ogmaShelf3D?.postMessage({JsonSerializer.Serialize(browserJson)});";
        _ = await InvokeScriptOnUiThreadAsync(script, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ThrowIfReady();
        return InvokeScriptOnUiThreadAsync(script, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RegisterSchemeHandlerAsync(
        string scheme,
        ISchemeHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfReady();
        if (!scheme.Equals("ogma", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only the local ogma scheme is supported by the shelf host.");
        }

        await DisposeAssetServerAsync().ConfigureAwait(false);
        _assetServer = await LoopbackAssetServer.StartAsync(handler, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ThrowIfReady();
        if (!uri.Scheme.Equals("ogma", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("assets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The shelf WebView may navigate only to local ogma assets.");
        }

        LoopbackAssetServer server = _assetServer ??
            throw new InvalidOperationException("The local shelf asset server has not been registered.");
        await RunOnUiThreadAsync(() => _webView.Source = server.ToBrowserUri(uri), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _webView.WebMessageReceived -= OnWebMessageReceived;
        _webView.NavigationStarted -= OnNavigationStarted;
        _webView.NewWindowRequested -= OnNewWindowRequested;
        _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView.AdapterDestroyed -= OnAdapterDestroyed;
        await DisposeAssetServerAsync().ConfigureAwait(false);
    }

    private async Task<string> InvokeScriptOnUiThreadAsync(string script, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Dispatcher.UIThread.InvokeAsync(
                async () => await _webView.InvokeScript(script).ConfigureAwait(true),
                DispatcherPriority.Normal,
                cancellationToken)
            .GetTask()
            .Unwrap()
            .ConfigureAwait(false) ?? string.Empty;
    }

    private static Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).GetTask();
    }

    private async Task DisposeAssetServerAsync()
    {
        if (_assetServer is null)
        {
            return;
        }

        LoopbackAssetServer server = _assetServer;
        _assetServer = null;
        await server.DisposeAsync().ConfigureAwait(false);
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Body))
        {
            RawMessageReceived?.Invoke(this, eventArgs.Body);
        }
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs eventArgs)
    {
        LoopbackAssetServer? server = _assetServer;
        eventArgs.Cancel = server is null || !server.IsAllowedBrowserUri(eventArgs.Request);
    }

    private static void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs eventArgs) =>
        eventArgs.Handled = true;

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs eventArgs)
    {
        if (!eventArgs.IsSuccess)
        {
            HostUnavailable?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAdapterDestroyed(object? sender, WebViewAdapterEventArgs eventArgs) =>
        HostUnavailable?.Invoke(this, EventArgs.Empty);

    private void ThrowIfReady()
    {
        ThrowIfDisposed();
        if (!_initialized)
        {
            throw new InvalidOperationException("The native shelf WebView has not been initialized.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class LoopbackAssetServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly ISchemeHandler _handler;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly string _token;
        private readonly Uri _browserRoot;
        private readonly Task _acceptLoop;
        private bool _disposed;

        private LoopbackAssetServer(TcpListener listener, ISchemeHandler handler)
        {
            _listener = listener;
            _handler = handler;
            _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(18)).ToLowerInvariant();
            _browserRoot = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{_token}/");
            _acceptLoop = AcceptLoopAsync(_shutdown.Token);
        }

        public static Task<LoopbackAssetServer> StartAsync(ISchemeHandler handler, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new LoopbackAssetServer(listener, handler));
        }

        public Uri ToBrowserUri(Uri uri)
        {
            string path = uri.AbsolutePath.TrimStart('/');
            const string assetsPrefix = "assets/";
            if (!path.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The shelf navigation URI must target an asset.");
            }

            return new Uri(_browserRoot, path);
        }

        public bool IsAllowedBrowserUri(Uri? uri) =>
            uri is not null &&
            uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            uri.Host.Equals(IPAddress.Loopback.ToString(), StringComparison.OrdinalIgnoreCase) &&
            uri.Port == ((IPEndPoint)_listener.LocalEndpoint).Port &&
            _browserRoot.IsBaseOf(uri);

        public string RewriteAssetUris(string json) => json.Replace(
            "ogma://assets/",
            _browserRoot.AbsoluteUri,
            StringComparison.OrdinalIgnoreCase);

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = HandleClientAsync(client, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    string? requestLine = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (requestLine is null)
                    {
                        return;
                    }

                    string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 3 || (parts[0] != "GET" && parts[0] != "HEAD"))
                    {
                        await WriteResponseAsync(stream, 405, "text/plain", [], cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    await DrainHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (!Uri.TryCreate(parts[1], UriKind.RelativeOrAbsolute, out Uri? requestUri) ||
                        requestUri.IsAbsoluteUri ||
                        !TryGetOgmaUri(requestUri, out Uri? ogmaUri) ||
                        ogmaUri is null ||
                        !_handler.CanHandle(ogmaUri))
                    {
                        await WriteResponseAsync(stream, 404, "text/plain", [], cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    SchemeResponse response = await _handler.HandleAsync(ogmaUri, cancellationToken).ConfigureAwait(false);
                    await WriteResponseAsync(
                            stream,
                            response.StatusCode,
                            response.ContentType,
                            parts[0] == "HEAD" ? [] : response.Body,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (IOException)
                {
                    // The browser may close a request while the view is detaching.
                }
            }
        }

        private bool TryGetOgmaUri(Uri requestUri, out Uri? ogmaUri)
        {
            ogmaUri = null;
            string path = requestUri.OriginalString.TrimStart('/');
            int queryIndex = path.IndexOfAny(['?', '#']);
            if (queryIndex >= 0)
            {
                path = path[..queryIndex];
            }

            string prefix = _token + "/assets/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length <= prefix.Length)
            {
                return false;
            }

            string query = queryIndex >= 0 ? requestUri.Query : string.Empty;
            return Uri.TryCreate("ogma://assets/" + path[prefix.Length..] + query, UriKind.Absolute, out ogmaUri);
        }

        private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = new List<byte>(256);
            while (buffer.Count < 8192)
            {
                byte[] one = new byte[1];
                int count = await stream.ReadAsync(one, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    return null;
                }

                if (one[0] == (byte)'\n')
                {
                    if (buffer.Count > 0 && buffer[^1] == (byte)'\r')
                    {
                        buffer.RemoveAt(buffer.Count - 1);
                    }

                    return Encoding.ASCII.GetString([.. buffer]);
                }

                buffer.Add(one[0]);
            }

            return null;
        }

        private static async Task DrainHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            while (true)
            {
                string? line = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line))
                {
                    return;
                }
            }
        }

        private static async Task WriteResponseAsync(
            Stream stream,
            int statusCode,
            string contentType,
            byte[] body,
            CancellationToken cancellationToken)
        {
            string reason = statusCode switch
            {
                200 => "OK",
                403 => "Forbidden",
                404 => "Not Found",
                405 => "Method Not Allowed",
                _ => "Error",
            };
            string headers = $"HTTP/1.1 {statusCode} {reason}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Cache-Control: no-store\r\n" +
                "X-Content-Type-Options: nosniff\r\n" +
                "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken).ConfigureAwait(false);
            if (body.Length > 0)
            {
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdown.Cancel();
            _listener.Stop();
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            _shutdown.Dispose();
        }
    }
}
