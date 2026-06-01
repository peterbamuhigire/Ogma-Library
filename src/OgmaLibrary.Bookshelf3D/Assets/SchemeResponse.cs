namespace OgmaLibrary.Bookshelf3D.Assets;

/// <summary>Response returned by an <see cref="ISchemeHandler"/>.</summary>
/// <param name="StatusCode">HTTP-like status code for the WebView response.</param>
/// <param name="ContentType">MIME type for the body.</param>
/// <param name="Body">Response bytes.</param>
public sealed record SchemeResponse(int StatusCode, string ContentType, byte[] Body)
{
    /// <summary>Creates a 403 response for rejected asset requests.</summary>
    public static SchemeResponse Forbidden { get; } = new(403, "text/plain", []);

    /// <summary>Creates a 404 response for absent asset requests.</summary>
    public static SchemeResponse NotFound { get; } = new(404, "text/plain", []);
}
