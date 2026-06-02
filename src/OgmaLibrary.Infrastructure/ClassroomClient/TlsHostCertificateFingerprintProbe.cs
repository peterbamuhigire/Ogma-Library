using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Reads the Host TLS leaf certificate and returns its SHA-256 fingerprint for TOFU.</summary>
internal sealed class TlsHostCertificateFingerprintProbe : IHostCertificateFingerprintProbe
{
    public async Task<string?> GetCertificateFingerprintAsync(
        ClassroomJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(request.Address, request.Port, cancellationToken).ConfigureAwait(false);
#pragma warning disable CA5359
        using var stream = new SslStream(
            tcp.GetStream(),
            leaveInnerStreamOpen: false,
            AcceptCertificateForFingerprintProbe);
#pragma warning restore CA5359
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = request.Address,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        };
        await stream.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);

        byte[]? certificateBytes = stream.RemoteCertificate?.GetRawCertData();
        if (certificateBytes is null || certificateBytes.Length == 0)
        {
            return null;
        }

        byte[] hash = SHA256.HashData(certificateBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [SuppressMessage(
        "Security",
        "CA5359:Do not disable certificate validation",
        Justification = "TOFU onboarding must read a self-signed classroom Host certificate before trust exists; fingerprint comparison enforces trust after extraction.")]
    private static bool AcceptCertificateForFingerprintProbe(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors) => true;
}
