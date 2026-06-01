namespace OgmaLibrary.Bookshelf3D.Assets;

/// <summary>Handles safe local WebView asset requests for custom URI schemes.</summary>
public interface ISchemeHandler
{
    /// <summary>Returns whether this handler can process the supplied URI.</summary>
    bool CanHandle(Uri uri);

    /// <summary>Returns a response for a local asset request.</summary>
    Task<SchemeResponse> HandleAsync(Uri uri, CancellationToken cancellationToken = default);
}
