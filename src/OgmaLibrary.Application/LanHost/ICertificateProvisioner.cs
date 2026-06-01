namespace OgmaLibrary.Application.LanHost;

/// <summary>Creates or loads the Host-mode certificate authority.</summary>
public interface ICertificateProvisioner
{
    /// <summary>Ensures a Host CA exists and returns its trust fingerprint.</summary>
    Task<CertificateProvisioningResult> EnsureProvisionedAsync(
        CancellationToken cancellationToken = default);
}

