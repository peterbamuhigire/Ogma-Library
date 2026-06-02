namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Typed client for the Phase 16 Library Host HTTP API.</summary>
public interface ILibraryHostClient
{
    /// <summary>Reads Host health metadata before enrolment.</summary>
    Task<LibraryHostHealth> GetHealthAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a Host session token after enrollment-code confirmation.</summary>
    Task<LibraryHostSession> IssueSessionAsync(
        ClassroomJoinRequest request,
        Guid profileId,
        ClassroomRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a page of Host catalogue summaries using an issued session token.</summary>
    Task<LibraryHostCataloguePage> GetCataloguePageAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostCatalogueQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one Host book detail projection using an issued session token.</summary>
    Task<LibraryHostBookDetail> GetBookAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default);

    /// <summary>Searches Host catalogue metadata using an issued session token.</summary>
    Task<LibraryHostSearchPage> SearchCatalogueAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        LibraryHostSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one rendered page PNG from a Host in page-render mode.</summary>
    Task<LibraryHostResource> GetPageRenderAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        int pageNumber,
        int widthPx,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a raw PDF stream from a Host in file-stream mode.</summary>
    Task<LibraryHostResource> GetFileStreamAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a Host sidecar asset from a projected asset URL.</summary>
    Task<LibraryHostResource> GetAssetAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string assetUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Uploads this profile's encrypted private-state sync blob to the Host.</summary>
    Task UploadProfileSyncBlobAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        EncryptedClassroomSyncBlob blob,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads this profile's encrypted private-state sync blob from the Host, when present.</summary>
    Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        CancellationToken cancellationToken = default);
}
