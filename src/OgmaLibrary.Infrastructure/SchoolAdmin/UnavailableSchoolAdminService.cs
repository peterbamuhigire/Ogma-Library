using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>Disabled Phase 18 implementation used until Host-local admin activation ships.</summary>
internal sealed class UnavailableSchoolAdminService :
    ILibraryPublishingService,
    ISharedShelfService,
    IProfileEnrollmentService,
    ISchoolAiPolicyService,
    ISchoolAiKeyProvider,
    IAiProxyEndpointHandler,
    IUsageDashboardService,
    IDpiaScreeningService
{
    private static readonly SchoolAiPolicy DisabledPolicy = new(
        DefaultTier: AiPrivacyTier.MetadataOnly,
        ContentAwareEnabled: false,
        PerStudentDailyTokenBudget: 0,
        ClassDailyTokenBudget: 0,
        PerStudentQueriesPerMinute: 0,
        AnswerModeEnabled: false);

    public Task<PublishedLibrary> PublishAsync(
        PublishLibraryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    public Task UnpublishAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryId);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    public Task<IReadOnlyList<PublishedLibrary>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<PublishedLibrary>>([]);
    }

    public Task<SharedShelf> SaveAsync(
        SaveSharedShelfRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    public Task DeleteAsync(string shelfId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    async Task<IReadOnlyList<SharedShelf>> ISharedShelfService.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.FromResult<IReadOnlyList<SharedShelf>>([]).ConfigureAwait(false);
    }

    public Task<EnrollmentToken> EnrollAsync(
        EnrollProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    async Task<IReadOnlyList<EnrolledProfile>> IProfileEnrollmentService.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.FromResult<IReadOnlyList<EnrolledProfile>>([]).ConfigureAwait(false);
    }

    public Task RevokeAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    public Task<EnrolledProfile?> RedeemTokenAsync(
        Guid profileId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<EnrolledProfile?>(null);
    }

    public Task<SchoolAiPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DisabledPolicy);
    }

    public Task SavePolicyAsync(SchoolAiPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    public Task<SchoolAiQuotaDecision> CheckAndReserveQuotaAsync(
        Guid profileId,
        int estimatedTokens,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        if (estimatedTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokens), estimatedTokens, "Estimated tokens cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SchoolAiQuotaDecision(
            IsAllowed: false,
            RemainingStudentTokens: 0,
            RemainingClassTokens: 0,
            ResetUtc: DateTimeOffset.UtcNow.Date.AddDays(1),
            Reason: "School administration is not enabled."));
    }

    public Task SaveKeyAsync(string providerId, char[] key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        Array.Clear(key);
        throw Disabled();
    }

    public Task<SchoolAiKeyStatus> GetStatusAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SchoolAiKeyStatus(providerId, IsConfigured: false, UpdatedUtc: null));
    }

    public Task DeleteKeyAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<AiProxyPayloadPreview> PreviewAsync(
        AiProxySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AiProxyPayloadPreview(
            AiPrivacyTier.MetadataOnly,
            new Dictionary<string, string>(),
            EstimatedCharacters: 0,
            RequiresConfirmation: true));
    }

    public Task<AiProxySearchResult> SearchAsync(
        AiProxySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw Disabled();
    }

    public Task<IReadOnlyList<UsageDashboardEntry>> GetSummaryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
        {
            throw new ArgumentException("End date must be after start date.", nameof(toUtc));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<UsageDashboardEntry>>([]);
    }

    public Task<DpiaScreeningResult> CheckAsync(
        DpiaScreeningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DpiaScreeningResult(
            DpiaScreeningDecision.Disqualified,
            "School DPIA policy is not configured.",
            DateTimeOffset.UtcNow));
    }

    private static InvalidOperationException Disabled() =>
        new("School administration is not enabled yet.");
}
