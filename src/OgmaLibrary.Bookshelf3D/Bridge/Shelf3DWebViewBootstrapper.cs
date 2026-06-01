using OgmaLibrary.Bookshelf3D.Assets;

namespace OgmaLibrary.Bookshelf3D.Bridge;

/// <summary>Initializes the native WebView host for the local 3D bookshelf scene.</summary>
public sealed class Shelf3DWebViewBootstrapper
{
    private readonly IWebViewBridge _bridge;
    private readonly ISchemeHandler _schemeHandler;
    private readonly Shelf3DAssetPublisher _assetPublisher;
    private readonly string _assetRoot;

    /// <summary>Initializes a new instance of <see cref="Shelf3DWebViewBootstrapper"/>.</summary>
    public Shelf3DWebViewBootstrapper(
        IWebViewBridge bridge,
        ISchemeHandler schemeHandler,
        Shelf3DAssetPublisher assetPublisher,
        string assetRoot)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(schemeHandler);
        ArgumentNullException.ThrowIfNull(assetPublisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);

        _bridge = bridge;
        _schemeHandler = schemeHandler;
        _assetPublisher = assetPublisher;
        _assetRoot = Path.GetFullPath(assetRoot);
    }

    /// <summary>
    /// Initializes the platform adapter, registers <c>ogma://</c>, publishes local
    /// web assets, and navigates to the bootstrap HTML.
    /// </summary>
    public async Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        await _bridge.InitializeAsync(host, cancellationToken).ConfigureAwait(false);
        await _bridge.RegisterSchemeHandlerAsync("ogma", _schemeHandler, cancellationToken).ConfigureAwait(false);
        Uri bootstrapUri = await _assetPublisher.PublishAsync(_assetRoot, cancellationToken).ConfigureAwait(false);
        await _bridge.NavigateAsync(bootstrapUri, cancellationToken).ConfigureAwait(false);
    }
}
