using System.Security.Cryptography.X509Certificates;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Loads the TLS certificate used by the Host-mode listener.</summary>
internal interface IHostServerCertificateProvider
{
    /// <summary>Returns a certificate with a private key suitable for HTTPS binding.</summary>
    Task<X509Certificate2> LoadOrCreateCertificateAsync(CancellationToken cancellationToken = default);
}
