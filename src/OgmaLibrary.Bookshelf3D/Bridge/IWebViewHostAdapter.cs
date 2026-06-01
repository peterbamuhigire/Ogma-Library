using OgmaLibrary.Bookshelf3D.Assets;

namespace OgmaLibrary.Bookshelf3D.Bridge;

/// <summary>
/// Minimal adapter that hides WebView2 and WKWebView APIs from the shared bridge
/// contract.
/// </summary>
public interface IWebViewHostAdapter
{
    /// <summary>Raised when the underlying WebView posts raw JSON to C#.</summary>
    event EventHandler<string>? RawMessageReceived;

    /// <summary>Initializes the native WebView surface.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Posts raw JSON to JavaScript.</summary>
    Task PostJsonAsync(string json, CancellationToken cancellationToken);

    /// <summary>Executes JavaScript and returns the native WebView result.</summary>
    Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken);

    /// <summary>Registers a local URI scheme handler with the native WebView.</summary>
    Task RegisterSchemeHandlerAsync(string scheme, ISchemeHandler handler, CancellationToken cancellationToken);

    /// <summary>Navigates the native WebView to a local bootstrap document.</summary>
    Task NavigateAsync(Uri uri, CancellationToken cancellationToken);
}
