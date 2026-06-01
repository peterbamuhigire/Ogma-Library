using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>
/// Phase 16 Host-mode coordinator. It starts the opt-in HTTPS listener only
/// when an administrator explicitly starts Host mode.
/// </summary>
internal sealed class LibraryHostService : ILibraryHostService
{
    private readonly IHostModeSettingsRepository _settings;
    private readonly ICertificateProvisioner _certificates;
    private readonly IMdnsAdvertiser _mdns;
    private readonly IClientSessionService _sessions;
    private readonly IHostModeListener _listener;
    private readonly ILanBindAddressSelector _bindAddressSelector;
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
        IClientSessionService sessions,
        IHostModeListener listener,
        ILanBindAddressSelector bindAddressSelector)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(mdns);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(bindAddressSelector);

        _settings = settings;
        _certificates = certificates;
        _mdns = mdns;
        _sessions = sessions;
        _listener = listener;
        _bindAddressSelector = bindAddressSelector;
    }

    /// <inheritdoc />
    public async Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        HostModeSettings settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        _status = _status with { State = LibraryHostState.Starting, Port = settings.Port, ErrorMessage = null };

        CertificateProvisioningResult certificate = await _certificates
            .EnsureProvisionedAsync(cancellationToken)
            .ConfigureAwait(false);
        string bindAddress = _bindAddressSelector.SelectBindAddress().ToString();
        await _listener.StartAsync(settings, certificate.Fingerprint, cancellationToken)
            .ConfigureAwait(false);
        await _mdns.StartAsync(
                new MdnsServiceRecord(
                    "_ogma-library._tcp.local",
                    settings.DisplayName,
                    settings.Port,
                    new Dictionary<string, string>
                    {
                        ["fp"] = certificate.Fingerprint,
                        ["addr"] = bindAddress,
                        ["requires-auth"] = "true",
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        _status = _status with
        {
            State = LibraryHostState.Running,
            CertificateFingerprint = certificate.Fingerprint,
            HostAddress = bindAddress,
            ConnectedClientCount = await _sessions.CountActiveAsync(cancellationToken).ConfigureAwait(false),
        };
        return _status;
    }

    /// <inheritdoc />
    public async Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        await _listener.StopAsync(cancellationToken).ConfigureAwait(false);
        await _sessions.RevokeAllAsync(cancellationToken).ConfigureAwait(false);
        await _mdns.StopAsync(cancellationToken).ConfigureAwait(false);
        _status = _status with { State = LibraryHostState.Stopped, ConnectedClientCount = 0, HostAddress = null };
        return _status;
    }

    /// <inheritdoc />
    public async Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_status.State == LibraryHostState.Running)
        {
            _status = _status with
            {
                ConnectedClientCount = await _sessions.CountActiveAsync(cancellationToken).ConfigureAwait(false),
            };
        }

        return _status;
    }
}

