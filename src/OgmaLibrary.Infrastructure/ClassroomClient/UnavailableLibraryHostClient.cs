using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Placeholder Host API client until discovery and TOFU are implemented.</summary>
internal sealed class UnavailableLibraryHostClient : ILibraryHostClient
{
    public Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostSession> IssueSessionAsync(
        ClassroomJoinRequest request,
        Guid profileId,
        ClassroomRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostCatalogueQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostBookDetail> GetBookAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostSearchPage> SearchCatalogueAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostResource> GetPageRenderAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        int pageNumber,
        int widthPx,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostResource> GetFileStreamAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }

    public Task<LibraryHostResource> GetAssetAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string assetUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Classroom Host client is not active yet.");
    }
}
