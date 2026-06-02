using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.SchoolAdmin;

/// <summary>Role values enforced by Host-local school administration routes.</summary>
public static class SchoolAdminAuthorization
{
    /// <summary>The only role allowed to mutate school administration state.</summary>
    public const string AdminRole = "admin";
}

/// <summary>Visibility scope for a shared classroom shelf.</summary>
public enum SharedShelfVisibility
{
    /// <summary>Visible to every enrolled student and teacher.</summary>
    AllStudents = 0,

    /// <summary>Visible only to teachers and administrators.</summary>
    TeacherOnly = 1,

    /// <summary>Visible only to explicitly assigned profile groups.</summary>
    SpecificGroups = 2,
}

/// <summary>Lifecycle status of an enrolled classroom profile.</summary>
public enum EnrollmentStatus
{
    /// <summary>The profile may authenticate and receive classroom sessions.</summary>
    Active = 0,

    /// <summary>The profile has been revoked and must not receive sessions.</summary>
    Revoked = 1,
}

/// <summary>Outcome of DPIA screening for an off-device AI action.</summary>
public enum DpiaScreeningDecision
{
    /// <summary>The configured legal basis and tier policy allow the action.</summary>
    Approved = 0,

    /// <summary>The action is blocked until an administrator configures policy.</summary>
    Disqualified = 1,
}

/// <summary>Published library/folder policy visible to classroom clients.</summary>
public sealed record PublishedLibrary(
    string LibraryId,
    string DisplayName,
    string SourcePath,
    AiPrivacyTier AiTier,
    bool IsPublished,
    DateTimeOffset UpdatedUtc);

/// <summary>Request to publish or update a library/folder policy.</summary>
public sealed record PublishLibraryRequest(
    string LibraryId,
    string DisplayName,
    string SourcePath,
    AiPrivacyTier AiTier);

/// <summary>Shared shelf curated by a school administrator.</summary>
public sealed record SharedShelf(
    string ShelfId,
    string Name,
    SharedShelfVisibility Visibility,
    IReadOnlyList<string> BookIds,
    IReadOnlyList<string> GroupIds,
    DateTimeOffset UpdatedUtc);

/// <summary>Request to create or replace a shared classroom shelf.</summary>
public sealed record SaveSharedShelfRequest(
    string ShelfId,
    string Name,
    SharedShelfVisibility Visibility,
    IReadOnlyList<string> BookIds,
    IReadOnlyList<string> GroupIds);

/// <summary>School-managed profile enrollment metadata.</summary>
public sealed record EnrolledProfile(
    Guid ProfileId,
    string DisplayName,
    string Role,
    EnrollmentStatus Status,
    int? BirthYear,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? RevokedUtc);

/// <summary>Request to enroll a student or teacher profile.</summary>
public sealed record EnrollProfileRequest(
    string DisplayName,
    string Role,
    int? BirthYear);

/// <summary>One-time token issued for profile enrollment.</summary>
public sealed record EnrollmentToken(
    Guid ProfileId,
    string Token,
    DateTimeOffset ExpiresUtc);

/// <summary>Classroom AI policy and budget settings.</summary>
public sealed record SchoolAiPolicy(
    AiPrivacyTier DefaultTier,
    bool ContentAwareEnabled,
    int PerStudentDailyTokenBudget,
    int ClassDailyTokenBudget,
    int PerStudentQueriesPerMinute,
    bool AnswerModeEnabled);

/// <summary>Result of checking and reserving classroom AI quota.</summary>
public sealed record SchoolAiQuotaDecision(
    bool IsAllowed,
    int RemainingStudentTokens,
    int RemainingClassTokens,
    DateTimeOffset ResetUtc,
    string? Reason);

/// <summary>Opaque status for a configured school AI provider key.</summary>
public sealed record SchoolAiKeyStatus(
    string ProviderId,
    bool IsConfigured,
    DateTimeOffset? UpdatedUtc);

/// <summary>Student classroom smart-search request proxied through the Host.</summary>
public sealed record AiProxySearchRequest(
    Guid ProfileId,
    string Query,
    string LibraryId,
    AiPrivacyTier RequestedTier,
    bool ConfirmedPayloadPreview);

/// <summary>Preview returned before an off-device classroom AI call is executed.</summary>
public sealed record AiProxyPayloadPreview(
    AiPrivacyTier Tier,
    IReadOnlyDictionary<string, string> MetadataFields,
    int EstimatedCharacters,
    bool RequiresConfirmation);

/// <summary>Grounded classroom AI search response.</summary>
public sealed record AiProxySearchResult(
    string Answer,
    IReadOnlyList<GroundedCitation> Citations,
    int TokensUsed,
    decimal EstimatedCostUsd,
    bool WasProviderCalled);

/// <summary>Citation verified against the Host catalogue.</summary>
public sealed record GroundedCitation(
    string BookId,
    string? Title,
    int? PageNumber);

/// <summary>Aggregated usage row for the admin dashboard.</summary>
public sealed record UsageDashboardEntry(
    Guid ProfileId,
    string DisplayName,
    int QueryCount,
    int TokensUsed,
    decimal EstimatedCostUsd,
    double QuotaPercent,
    DateTimeOffset? LastQueryUtc);

/// <summary>Request to screen an off-device classroom AI action.</summary>
public sealed record DpiaScreeningRequest(
    Guid ProfileId,
    AiPrivacyTier Tier,
    string PayloadScope,
    int? BirthYear);

/// <summary>DPIA screening result recorded before the provider is called.</summary>
public sealed record DpiaScreeningResult(
    DpiaScreeningDecision Decision,
    string Reason,
    DateTimeOffset CheckedUtc);
