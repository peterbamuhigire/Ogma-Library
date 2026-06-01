using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Messages;

namespace OgmaLibrary.Bookshelf3D.Bridge;

/// <summary>Shared bridge behavior for platform-specific WebView hosts.</summary>
public abstract class WebViewBridgeBase : IWebViewBridge
{
    private IWebViewHostAdapter? _host;

    /// <inheritdoc />
    public event EventHandler<InboundMessage>? MessageReceived;

    /// <inheritdoc />
    public async Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (_host is not null)
        {
            _host.RawMessageReceived -= OnRawMessageReceived;
        }

        _host = host;
        _host.RawMessageReceived += OnRawMessageReceived;
        await _host.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task PostMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return RequireHost().PostJsonAsync(OutboundMessageJsonSerializer.Serialize(message), cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        return RequireHost().ExecuteScriptAsync(script, cancellationToken);
    }

    /// <inheritdoc />
    public Task RegisterSchemeHandlerAsync(
        string scheme,
        ISchemeHandler handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentNullException.ThrowIfNull(handler);
        return RequireHost().RegisterSchemeHandlerAsync(scheme, handler, cancellationToken);
    }

    /// <inheritdoc />
    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return RequireHost().NavigateAsync(uri, cancellationToken);
    }

    private IWebViewHostAdapter RequireHost() =>
        _host ?? throw new InvalidOperationException("The WebView bridge has not been initialized.");

    private void OnRawMessageReceived(object? sender, string json)
    {
        if (!InboundMessageParser.TryParse(json, out InboundMessage? message, out _) || message is null)
        {
            return;
        }

        InboundMessageValidationResult validation = InboundMessageValidator.Validate(message);
        if (!validation.ShouldDispatch)
        {
            return;
        }

        MessageReceived?.Invoke(this, message);
    }
}
