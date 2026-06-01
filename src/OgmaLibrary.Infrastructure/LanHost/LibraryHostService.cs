using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>
/// Phase 16 Host-mode coordinator scaffold. It does not bind a network listener
/// until the Kestrel HTTPS adapter lands behind this bounded-context boundary.
/// </summary>
internal sealed class LibraryHostService : ILibraryHostService
{
    private readonly IHostModeSettingsRepository _settings;
    private readonly ICertificateProvisioner _certificates;
    private readonly IMdnsAdvertiser _mdns;
    private readonly IClientSessionService _sessions;
    private LibraryHostStatus _status = new(
        LibraryHostState.Stopped,
        Port: 7473,
        ConnectedClientCount: 0,
        CertificateFingerprint: null,
        ErrorMessage: null);

    public LibraryHostService(
        IHostModeSettingsRepository settings,
        ICertificateProvisioner certificates,
        IMdnsAdvertiser mdns,
        IClientSessionService sessions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(mdns);
        ArgumentNullException.ThrowIfNull(sessions);

        _settings = settings;
        _certificates = certificates;
        _mdns = mdns;
        _sessions = sessions;
    }

    /// <inheritdoc />
    public async Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        HostModeSettings settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        _status = _status with { State = LibraryHostState.Starting, Port = settings.Port, ErrorMessage = null };

        CertificateProvisioningResult certificate = await _certificates
            .EnsureProvisionedAsync(cancellationToken)
            .ConfigureAwait(false);
        await _mdns.StartAsync(
                new MdnsServiceRecord(
                    "_ogma-library._tcp.local",
                    settings.DisplayName,
                    settings.Port,
                    new Dictionary<string, string>
                    {
                        ["fp"] = certificate.Fingerprint,
                        ["requires-auth"] = "true",
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        _status = _status with
        {
            State = LibraryHostState.Running,
            CertificateFingerprint = certificate.Fingerprint,
        };
        return _status;
    }

    /// <inheritdoc />
    public async Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        await _mdns.StopAsync(cancellationToken).ConfigureAwait(false);
        await _sessions.RevokeAllAsync(cancellationToken).ConfigureAwait(false);
        _status = _status with { State = LibraryHostState.Stopped, ConnectedClientCount = 0 };
        return _status;
    }

    /// <inheritdoc />
    public Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_status);
    }
}

