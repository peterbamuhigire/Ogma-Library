namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Extracts a Host TLS certificate fingerprint before TOFU trust evaluation.</summary>
public interface IHostCertificateFingerprintProbe
{
    /// <summary>Returns the SHA-256 fingerprint of the Host TLS certificate, or null when unavailable.</summary>
    Task<string?> GetCertificateFingerprintAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default);
}
