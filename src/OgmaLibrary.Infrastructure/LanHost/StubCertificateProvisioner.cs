using System.Security.Cryptography;
using System.Text;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Deterministic scaffold certificate provisioner until OS-backed CA storage lands.</summary>
internal sealed class StubCertificateProvisioner : ICertificateProvisioner
{
    private const string Seed = "Ogma-Library-Phase16-LanHost-Scaffold";

    /// <inheritdoc />
    public Task<CertificateProvisioningResult> EnsureProvisionedAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Seed)));
        return Task.FromResult(new CertificateProvisioningResult(
            fingerprint,
            DateTimeOffset.UtcNow.AddYears(5)));
    }
}

