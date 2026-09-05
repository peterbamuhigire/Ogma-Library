using OgmaLibrary.App.Configuration;
using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;

namespace OgmaLibrary.App.ViewModels.Shelf3D;

/// <summary>Composes the shared shelf bootstrap against the configured library sidecar.</summary>
public sealed class Shelf3DHostCoordinator : IShelf3DHostCoordinator
{
    private readonly IWebViewBridge _bridge;
    private readonly OgmaRuntimeOptions _options;

    /// <summary>Initializes a coordinator for the application's configured library.</summary>
    public Shelf3DHostCoordinator(IWebViewBridge bridge, OgmaRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(options);
        _bridge = bridge;
        _options = options;
    }

    /// <inheritdoc />
    public Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        string assetRoot = Path.Combine(_options.LibraryRoot, ".ogma");
        var bootstrapper = new Shelf3DWebViewBootstrapper(
            _bridge,
            new OgmaSchemeHandler(assetRoot),
            new Shelf3DAssetPublisher(),
            assetRoot);
        return bootstrapper.InitializeAsync(host, cancellationToken);
    }
}
