using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Runtime library mode for an Ogma installation.</summary>
public enum LibraryRuntimeMode
{
    /// <summary>Local single-user catalogue mode; this is the default.</summary>
    Standalone = 0,

    /// <summary>Client mode that connects to a LAN Library Host.</summary>
    ConnectToHost = 1,
}

/// <summary>Classroom role assigned to a local profile/session.</summary>
public enum ClassroomRole
{
    /// <summary>Can browse, read, and keep private reading state.</summary>
    Student = 0,

    /// <summary>Can browse and read; curation powers arrive in Phase 18.</summary>
    Teacher = 1,

    /// <summary>Can browse and read without persistent private state.</summary>
    Guest = 2,
}

/// <summary>Persisted runtime mode settings.</summary>
public sealed record ClassroomModeSettings(LibraryRuntimeMode Mode);

/// <summary>Persisted Client-mode private sync settings.</summary>
public sealed record ClassroomSyncSettings(
    bool IsEnabled = false,
    bool SyncOnReconnect = false);

/// <summary>Runtime Client-mode connectivity status.</summary>
public sealed record ClassroomConnectivityStatus(
    bool IsOnline,
    DateTimeOffset UpdatedUtc,
    string? Message = null);

/// <summary>Runtime active Host connection used by Client-mode reader and catalogue calls.</summary>
public sealed record ClassroomHostConnection(
    ClassroomJoinRequest Request,
    string SessionToken,
    DateTimeOffset ConnectedUtc);

/// <summary>Request to connect this installation to a classroom Host.</summary>
public sealed record ClassroomConnectionRequest(
    ClassroomJoinRequest JoinRequest,
    bool AcceptFirstUseTrust = false,
    string? PresentedFingerprint = null,
    Guid? ProfileId = null,
    string? ProfileDisplayName = null,
    ClassroomRole Role = ClassroomRole.Student,
    bool UseGuestProfile = false,
    TimeSpan? SessionLifetime = null);

/// <summary>Outcome of a Client-mode Host connection attempt.</summary>
public sealed record ClassroomConnectionResult(
    bool IsConnected,
    HostTrustState TrustState,
    ClassroomProfile? Profile,
    ClassroomHostConnection? Connection,
    string? ErrorMessage = null);

/// <summary>Local classroom profile metadata.</summary>
public sealed record ClassroomProfile(
    Guid ProfileId,
    string DisplayName,
    ClassroomRole Role,
    bool IsGuest);

/// <summary>Request to create a persistent classroom profile.</summary>
public sealed record CreateClassroomProfileRequest(
    string DisplayName,
    ClassroomRole Role);

/// <summary>LAN Host discovered through mDNS, QR payload, or manual entry.</summary>
public sealed record DiscoveredClassroomHost(
    string HostId,
    string DisplayName,
    string Address,
    int Port,
    string CertificateFingerprint,
    IReadOnlyDictionary<string, string> Txt);

/// <summary>Parsed join payload used by the client TOFU flow.</summary>
public sealed record ClassroomJoinRequest(
    string Address,
    int Port,
    string CertificateFingerprint,
    string? DisplayName = null,
    string? EnrollmentCode = null,
    string AuthMethod = "enrollment-code");

/// <summary>Current sync state for the active classroom profile.</summary>
public sealed record ClassroomSyncStatus(
    bool IsEnabled,
    bool IsRunning,
    DateTimeOffset? LastSyncedUtc,
    int ConflictCount,
    string? ErrorMessage);

/// <summary>Student choice for a pending private-state sync conflict.</summary>
public enum ClassroomSyncConflictResolution
{
    /// <summary>Keep the local row and discard the pending remote version.</summary>
    KeepLocal = 0,

    /// <summary>Apply the remote row received from the classroom Host.</summary>
    KeepServer = 1,
}

/// <summary>Cached classroom resource payload.</summary>
public sealed record OfflineCacheEntry(
    string HostId,
    string ResourceKey,
    string? ETag,
    byte[] Content,
    DateTimeOffset StoredUtc,
    string ContentType = "application/octet-stream");

/// <summary>Lightweight Host health response consumed before full enrolment.</summary>
public sealed record LibraryHostHealth(
    string DisplayName,
    string CertificateFingerprint,
    string ContentMode);

/// <summary>Session token issued by a classroom Host.</summary>
public sealed record LibraryHostSession(
    string Token,
    DateTimeOffset ExpiresUtc);

/// <summary>Catalogue query sent to a classroom Host.</summary>
public sealed record LibraryHostCatalogueQuery(
    string? Title = null,
    string? Author = null,
    string? ShelfId = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>Page of catalogue books returned by a classroom Host.</summary>
public sealed record LibraryHostCataloguePage(
    IReadOnlyList<LibraryHostBookSummary> Items,
    int Page,
    int PageSize,
    int ReturnedCount,
    bool HasMore);

/// <summary>Book summary projected by a classroom Host.</summary>
public sealed record LibraryHostBookSummary(
    string BookId,
    string? Title,
    IReadOnlyList<string> Authors,
    int Status,
    int? Rating,
    IReadOnlyList<string> ShelfIds,
    double? ReadingProgressPct,
    bool IsAvailable,
    int? Year,
    string? ContentHash,
    LibraryHostAssetLinks Assets);

/// <summary>Full book detail projected by a classroom Host.</summary>
public sealed record LibraryHostBookDetail(
    string BookId,
    string? Title,
    IReadOnlyList<string> Authors,
    int? Year,
    string? Isbn,
    string? Doi,
    int? Rating,
    int Status,
    string? ContentHash,
    long? SizeBytes,
    LibraryHostReadingProgress? ReadingProgress,
    int Annotations,
    IReadOnlyList<LibraryHostMetadataField> MetadataFields,
    LibraryHostReadingMemorySummary? ReadingMemory,
    bool IsOcrDerived,
    bool IsPasswordProtected,
    LibraryHostAssetLinks Assets);

/// <summary>Host-projected reading progress for a book.</summary>
public sealed record LibraryHostReadingProgress(
    string BookId,
    int CurrentPage,
    double CompletionPct,
    DateTimeOffset? LastReadUtc,
    int Status);

/// <summary>Host-projected metadata field with provenance.</summary>
public sealed record LibraryHostMetadataField(
    string FieldName,
    string? Value,
    string? Source,
    double? Confidence,
    bool IsOverridden);

/// <summary>Host-projected reading memory summary.</summary>
public sealed record LibraryHostReadingMemorySummary(
    int? Disposition,
    string? KeyInsight,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Metadata search query sent to a classroom Host.</summary>
public sealed record LibraryHostSearchQuery(
    string? Query,
    int PageSize = 20);

/// <summary>Metadata search page returned by a classroom Host.</summary>
public sealed record LibraryHostSearchPage(
    string Query,
    IReadOnlyList<LibraryHostSearchResult> Items,
    int ReturnedCount,
    bool HasMore);

/// <summary>One Host metadata search result.</summary>
public sealed record LibraryHostSearchResult(
    string BookId,
    string? Title,
    string? Author,
    int Score,
    IReadOnlyList<string> MatchedFields);

/// <summary>Student smart-search request sent to a classroom Host AI proxy.</summary>
public sealed record LibraryHostAiSearchRequest(
    Guid ProfileId,
    string Query,
    string LibraryId,
    AiPrivacyTier RequestedTier,
    bool ConfirmedPayloadPreview);

/// <summary>Host AI payload preview that must be confirmed before provider egress.</summary>
public sealed record LibraryHostAiPayloadPreview(
    AiPrivacyTier Tier,
    IReadOnlyDictionary<string, string> MetadataFields,
    int EstimatedCharacters,
    bool RequiresConfirmation);

/// <summary>Grounded classroom AI search result returned by the Host.</summary>
public sealed record LibraryHostAiSearchResult(
    string Answer,
    IReadOnlyList<LibraryHostAiCitation> Citations,
    int TokensUsed,
    decimal EstimatedCostUsd,
    bool WasProviderCalled);

/// <summary>Host-verified citation for one classroom AI answer.</summary>
public sealed record LibraryHostAiCitation(
    string BookId,
    string? Title,
    int? PageNumber);

/// <summary>Asset links projected by a classroom Host.</summary>
public sealed record LibraryHostAssetLinks(
    string? CoverUrl,
    string? SpineUrl,
    string? ThumbnailUrl);

/// <summary>Binary resource returned by a classroom Host.</summary>
public sealed record LibraryHostResource(
    string ResourceKey,
    string ContentType,
    string? ETag,
    byte[] Content);

/// <summary>TOFU trust state for a Host certificate fingerprint.</summary>
public enum HostTrustState
{
    /// <summary>No previous pin exists; the user must explicitly accept.</summary>
    FirstUse = 0,

    /// <summary>The presented fingerprint matches the stored Host pin.</summary>
    Trusted = 1,

    /// <summary>The presented fingerprint does not match the expected or stored Host pin.</summary>
    Mismatch = 2,
}

/// <summary>Persisted Host trust pin.</summary>
public sealed record HostTrustPin(
    string HostKey,
    string Address,
    int Port,
    string CertificateFingerprint,
    DateTimeOffset PinnedUtc);

/// <summary>Result of evaluating a Host certificate against TOFU pins.</summary>
public sealed record HostTrustEvaluation(
    ClassroomJoinRequest Request,
    HostTrustState State,
    string PresentedFingerprint,
    string? PinnedFingerprint);

/// <summary>Private per-student reading progress for one Host book.</summary>
public sealed record StudentReadingProgress(
    string HostId,
    string BookId,
    int LastPage,
    double LastOffsetY,
    DateTimeOffset UpdatedUtc);

/// <summary>Private per-student annotation for one Host book.</summary>
public sealed record StudentAnnotation(
    string Id,
    string HostId,
    string BookId,
    int PageNumber,
    string Type,
    string? Color,
    string? Body,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    bool IsDeleted = false);

/// <summary>Pending private annotation conflict awaiting student choice.</summary>
public sealed record StudentAnnotationConflict(
    string HostId,
    StudentAnnotation LocalAnnotation,
    StudentAnnotation RemoteAnnotation,
    DateTimeOffset DetectedUtc);

/// <summary>Private per-student bookmark for one Host book.</summary>
public sealed record StudentBookmark(
    string Id,
    string HostId,
    string BookId,
    int PageNumber,
    string? Label,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    bool IsDeleted = false);

/// <summary>Private per-student AI query history entry.</summary>
public sealed record StudentAiHistoryEntry(
    string Id,
    string HostId,
    string Query,
    string? ResponseSummary,
    string Tier,
    DateTimeOffset CreatedUtc,
    bool IsDeleted = false);

/// <summary>Private per-student sync state for one Host.</summary>
public sealed record StudentSyncState(
    string HostId,
    DateTimeOffset? LastSyncedUtc,
    string? LastSyncBlobHash,
    int ConflictCount);
