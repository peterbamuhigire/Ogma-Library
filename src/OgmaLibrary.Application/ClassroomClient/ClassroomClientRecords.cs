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
    string CertificateFingerprint);

/// <summary>Current sync state for the active classroom profile.</summary>
public sealed record ClassroomSyncStatus(
    bool IsEnabled,
    bool IsRunning,
    DateTimeOffset? LastSyncedUtc,
    int ConflictCount,
    string? ErrorMessage);

/// <summary>Cached classroom resource payload.</summary>
public sealed record OfflineCacheEntry(
    string HostId,
    string ResourceKey,
    string? ETag,
    byte[] Content,
    DateTimeOffset StoredUtc);

/// <summary>Lightweight Host health response consumed before full enrolment.</summary>
public sealed record LibraryHostHealth(
    string DisplayName,
    string CertificateFingerprint,
    string ContentMode);
