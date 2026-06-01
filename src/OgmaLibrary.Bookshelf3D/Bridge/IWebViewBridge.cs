using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Messages;

namespace OgmaLibrary.Bookshelf3D.Bridge;

/// <summary>Platform-neutral bridge for the WebView-hosted Three.js bookshelf.</summary>
public interface IWebViewBridge
{
    /// <summary>Raised after an inbound JavaScript message has been parsed and validated.</summary>
    event EventHandler<InboundMessage>? MessageReceived;

    /// <summary>Initializes the bridge with the platform WebView host adapter.</summary>
    Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default);

    /// <summary>Posts a typed C# to JavaScript message.</summary>
    Task PostMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default);

    /// <summary>Executes JavaScript in the WebView and returns the raw platform result.</summary>
    Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>Registers a local asset scheme handler such as <c>ogma://</c>.</summary>
    Task RegisterSchemeHandlerAsync(string scheme, ISchemeHandler handler, CancellationToken cancellationToken = default);

    /// <summary>Navigates the WebView to a local document served by the registered scheme.</summary>
    Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default);
}
