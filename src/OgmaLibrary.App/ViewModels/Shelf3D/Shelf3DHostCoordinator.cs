using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;

namespace OgmaLibrary.App.ViewModels.Shelf3D;

/// <summary>
/// Composes the shared shelf bootstrap against the app-data asset store (Sept-23
/// Phase 05, K20): the 3D shelf reads the same covers and spines as the grid.
/// </summary>
public sealed class Shelf3DHostCoordinator : IShelf3DHostCoordinator
{
    private readonly IWebViewBridge _bridge;
    private readonly IAssetLocator _assets;

    /// <summary>Initializes a coordinator for the application's asset store.</summary>
    public Shelf3DHostCoordinator(IWebViewBridge bridge, IAssetLocator assets)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(assets);
        _bridge = bridge;
        _assets = assets;
    }

    /// <inheritdoc />
    public Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        string assetRoot = Path.Combine(_assets.AssetStoreRoot, ".ogma");
        var bootstrapper = new Shelf3DWebViewBootstrapper(
            _bridge,
            new OgmaSchemeHandler(assetRoot),
            new Shelf3DAssetPublisher(),
            assetRoot);
        return bootstrapper.InitializeAsync(host, cancellationToken);
    }
}
