using OgmaLibrary.Bookshelf3D.Bridge;

namespace OgmaLibrary.App.ViewModels.Shelf3D;

/// <summary>Initializes the optional native 3D host without coupling the view model to Avalonia.</summary>
public interface IShelf3DHostCoordinator
{
    /// <summary>Initializes a native host and navigates it to the packaged local shelf.</summary>
    Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default);
}
