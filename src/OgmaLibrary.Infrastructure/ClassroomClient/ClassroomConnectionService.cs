using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Coordinates the Client-mode connection flow after Host discovery/manual entry.</summary>
internal sealed class ClassroomConnectionService : IClassroomConnectionService
{
    private static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromHours(8);

    private readonly IHostTrustService _trustService;
    private readonly IProfileService _profileService;
    private readonly ILibraryHostClient _hostClient;
    private readonly IClassroomHostConnectionService _connectionService;
    private readonly IClassroomModeService _modeService;

    public ClassroomConnectionService(
        IHostTrustService trustService,
        IProfileService profileService,
        ILibraryHostClient hostClient,
        IClassroomHostConnectionService connectionService,
        IClassroomModeService modeService)
    {
        _trustService = trustService ?? throw new ArgumentNullException(nameof(trustService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _hostClient = hostClient ?? throw new ArgumentNullException(nameof(hostClient));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
    }

    public async Task<ClassroomConnectionResult> ConnectAsync(
        ClassroomConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.JoinRequest);
        cancellationToken.ThrowIfCancellationRequested();

        string presentedFingerprint = await ResolvePresentedFingerprintAsync(request, cancellationToken)
            .ConfigureAwait(false);
        HostTrustEvaluation trust = request.AcceptFirstUseTrust
            ? await _trustService
                .AcceptAsync(request.JoinRequest, presentedFingerprint, cancellationToken)
                .ConfigureAwait(false)
            : await _trustService
                .EvaluateAsync(request.JoinRequest, presentedFingerprint, cancellationToken)
                .ConfigureAwait(false);

        if (trust.State == HostTrustState.FirstUse)
        {
            return new ClassroomConnectionResult(
                IsConnected: false,
                trust.State,
                Profile: null,
                Connection: null,
                ErrorMessage: "Host certificate must be accepted before connecting.");
        }

        if (trust.State == HostTrustState.Mismatch)
        {
            return new ClassroomConnectionResult(
                IsConnected: false,
                trust.State,
                Profile: null,
                Connection: null,
                ErrorMessage: "Host certificate fingerprint does not match the trusted pin.");
        }

        ClassroomProfile? profile = await ResolveProfileAsync(request, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return new ClassroomConnectionResult(
                IsConnected: false,
                trust.State,
                Profile: null,
                Connection: null,
                ErrorMessage: "Select or create a classroom profile before connecting.");
        }

        LibraryHostSession session = await _hostClient
            .IssueSessionAsync(
                request.JoinRequest,
                profile.ProfileId,
                profile.Role,
                request.SessionLifetime ?? DefaultSessionLifetime,
                cancellationToken)
            .ConfigureAwait(false);

        if (!profile.IsGuest)
        {
            await _profileService
                .StoreSessionTokenAsync(profile.ProfileId, session.Token, cancellationToken)
                .ConfigureAwait(false);
        }

        var connection = new ClassroomHostConnection(
            request.JoinRequest,
            session.Token,
            DateTimeOffset.UtcNow);
        await _connectionService.SetActiveAsync(connection, cancellationToken).ConfigureAwait(false);
        await _modeService
            .SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost), cancellationToken)
            .ConfigureAwait(false);
        await _modeService
            .SetConnectivityAsync(
                new ClassroomConnectivityStatus(
                    IsOnline: true,
                    UpdatedUtc: connection.ConnectedUtc,
                    Message: request.JoinRequest.DisplayName is { Length: > 0 } displayName
                        ? $"Connected to {displayName}"
                        : "Connected to classroom Host"),
                cancellationToken)
            .ConfigureAwait(false);

        return new ClassroomConnectionResult(
            IsConnected: true,
            trust.State,
            profile,
            connection);
    }

    private async Task<string> ResolvePresentedFingerprintAsync(
        ClassroomConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PresentedFingerprint))
        {
            return request.PresentedFingerprint;
        }

        LibraryHostHealth health = await _hostClient
            .GetHealthAsync(request.JoinRequest, cancellationToken)
            .ConfigureAwait(false);
        return health.CertificateFingerprint;
    }

    private async Task<ClassroomProfile?> ResolveProfileAsync(
        ClassroomConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProfileId is Guid profileId)
        {
            await _profileService.SelectAsync(profileId, cancellationToken).ConfigureAwait(false);
            return await _profileService.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        }

        ClassroomProfile? active = await _profileService.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            return active;
        }

        if (request.UseGuestProfile)
        {
            return await _profileService.CreateGuestSessionAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(request.ProfileDisplayName))
        {
            return await _profileService
                .CreateAsync(
                    new CreateClassroomProfileRequest(request.ProfileDisplayName, request.Role),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }
}
