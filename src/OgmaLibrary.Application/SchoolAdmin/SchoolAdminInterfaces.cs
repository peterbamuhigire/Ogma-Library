namespace OgmaLibrary.Application.SchoolAdmin;

/// <summary>Publishes and unpublishes Host-local library folders for classroom clients.</summary>
public interface ILibraryPublishingService
{
    /// <summary>Creates or updates a published library policy.</summary>
    Task<PublishedLibrary> PublishAsync(
        PublishLibraryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a library from the client-visible catalogue projection.</summary>
    Task UnpublishAsync(string libraryId, CancellationToken cancellationToken = default);

    /// <summary>Lists school-published library policies.</summary>
    Task<IReadOnlyList<PublishedLibrary>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Manages administrator-curated shared classroom shelves.</summary>
public interface ISharedShelfService
{
    /// <summary>Creates or replaces a shared shelf definition.</summary>
    Task<SharedShelf> SaveAsync(
        SaveSharedShelfRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a shared shelf definition.</summary>
    Task DeleteAsync(string shelfId, CancellationToken cancellationToken = default);

    /// <summary>Lists shared shelves visible to administrators.</summary>
    Task<IReadOnlyList<SharedShelf>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Enrolls, edits, and revokes school-managed classroom profiles.</summary>
public interface IProfileEnrollmentService
{
    /// <summary>Enrolls a profile and returns a one-time enrollment token.</summary>
    Task<EnrollmentToken> EnrollAsync(
        EnrollProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists enrolled and revoked profiles.</summary>
    Task<IReadOnlyList<EnrolledProfile>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Revokes a profile and prevents new sessions.</summary>
    Task RevokeAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Redeems a one-time enrollment token and consumes it on success.</summary>
    Task<EnrolledProfile?> RedeemTokenAsync(
        Guid profileId,
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>Owns classroom AI tier, quota, and rate-limit policy.</summary>
public interface ISchoolAiPolicyService
{
    /// <summary>Gets the current classroom AI policy.</summary>
    Task<SchoolAiPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the current classroom AI policy.</summary>
    Task SavePolicyAsync(SchoolAiPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>Atomically checks and reserves quota before an AI provider call.</summary>
    Task<SchoolAiQuotaDecision> CheckAndReserveQuotaAsync(
        Guid profileId,
        int estimatedTokens,
        CancellationToken cancellationToken = default);
}

/// <summary>Stores and retrieves school-owned AI provider key status on the Host.</summary>
public interface ISchoolAiKeyProvider
{
    /// <summary>Saves a provider key from mutable memory and clears the input buffer.</summary>
    Task SaveKeyAsync(string providerId, char[] key, CancellationToken cancellationToken = default);

    /// <summary>Returns whether a provider key is configured without exposing the key value.</summary>
    Task<SchoolAiKeyStatus> GetStatusAsync(string providerId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a configured provider key.</summary>
    Task DeleteKeyAsync(string providerId, CancellationToken cancellationToken = default);
}

/// <summary>Handles Host-side classroom AI search proxy requests.</summary>
public interface IAiProxyEndpointHandler
{
    /// <summary>Builds the payload preview that must be confirmed before provider egress.</summary>
    Task<AiProxyPayloadPreview> PreviewAsync(
        AiProxySearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a confirmed classroom AI search through the Host gateway.</summary>
    Task<AiProxySearchResult> SearchAsync(
        AiProxySearchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Aggregates AI usage for the school administration dashboard.</summary>
public interface IUsageDashboardService
{
    /// <summary>Returns per-profile usage summaries for a date range.</summary>
    Task<IReadOnlyList<UsageDashboardEntry>> GetSummaryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Deletes erasable classroom AI history under administrator control.</summary>
public interface ISchoolAiHistoryManagementService
{
    /// <summary>Purges institution-wide AI query history and usage-ledger rows without deleting immutable audit rows.</summary>
    Task<SchoolAiHistoryPurgeResult> PurgeInstitutionHistoryAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Creates verified school-data backups and rehearses restoration without replacing the live catalogue.</summary>
public interface ISchoolBackupService
{
    /// <summary>Creates an online SQLite backup in an administrator-selected protected directory.</summary>
    Task<SchoolBackupResult> CreateBackupAsync(
        string destinationDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>Restores a backup into an isolated temporary database and verifies its logical contents.</summary>
    Task<SchoolRestoreRehearsalResult> RehearseRestoreAsync(
        string backupPath,
        CancellationToken cancellationToken = default);
}

/// <summary>Performs DPIA gating before off-device classroom AI calls.</summary>
public interface IDpiaScreeningService
{
    /// <summary>Checks whether the requested AI action is approved for the profile and tier.</summary>
    Task<DpiaScreeningResult> CheckAsync(
        DpiaScreeningRequest request,
        CancellationToken cancellationToken = default);
}
