using System.Security.Cryptography;
using System.Text;
using OgmaLibrary.Application.Security;
using OgmaLibrary.Infrastructure.Security;

namespace OgmaLibrary.Tests.Security;

/// <summary>Trust-chain tests for update descriptors and packages.</summary>
public sealed class RsaUpdateVerifierTests
{
    [Fact]
    public void VerifyDescriptor_RejectsAlteredDescriptor()
    {
        using RSA key = RSA.Create(4096);
        string descriptor = "{\"version\":\"0.1.0\",\"sha256\":\"abc\"}";
        string signature = Convert.ToBase64String(key.SignData(
            Encoding.UTF8.GetBytes(descriptor),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));
        using var verifier = new RsaUpdateVerifier(key.ExportSubjectPublicKeyInfoPem());

        Assert.True(verifier.VerifyDescriptor(descriptor, signature));
        Assert.False(verifier.VerifyDescriptor(descriptor.Replace("0.1.0", "0.1.1", StringComparison.Ordinal), signature));
    }

    [Fact]
    public void VerifyPackage_RejectsAlteredPackage()
    {
        using RSA key = RSA.Create(4096);
        using var verifier = new RsaUpdateVerifier(key.ExportSubjectPublicKeyInfoPem());
        byte[] package = "trusted package"u8.ToArray();
        string expected = Convert.ToHexString(SHA256.HashData(package));

        using var valid = new MemoryStream(package);
        Assert.True(verifier.VerifyPackage(valid, expected));

        package[0] ^= 0xFF;
        using var altered = new MemoryStream(package);
        Assert.False(verifier.VerifyPackage(altered, expected));
    }

    [Fact]
    public void TryVerify_RejectsTamperedDescriptorAndInvalidShape()
    {
        using RSA key = RSA.Create(4096);
        using var verifier = new RsaUpdateVerifier(key.ExportSubjectPublicKeyInfoPem());
        string descriptor = "{\"schema\":\"ogma-release-v1\",\"releaseId\":\"rc-1\",\"version\":\"0.1.0-beta.1\",\"platform\":\"windows\",\"runtimeIdentifier\":\"win-x64\",\"artifactName\":\"OgmaLibrary-win-x64.zip\",\"artifactSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"signatureAlgorithm\":\"RSA-PSS-SHA256\",\"publicKeyId\":\"production-2026\"}";
        string signature = Convert.ToBase64String(key.SignData(
            Encoding.UTF8.GetBytes(descriptor),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));

        Assert.True(ReleaseDescriptorVerifier.TryVerify(descriptor, signature, verifier, out ReleaseDescriptor? parsed));
        Assert.Equal("win-x64", parsed?.RuntimeIdentifier);
        Assert.False(ReleaseDescriptorVerifier.TryVerify(
            descriptor.Replace("0123456789", "fedcba9876", StringComparison.Ordinal),
            signature,
            verifier,
            out _));
        Assert.False(ReleaseDescriptor.TryParse(
            descriptor.Replace("\"artifactName\":\"OgmaLibrary-win-x64.zip\"", "\"artifactName\":\"..\\\\escape.zip\"", StringComparison.Ordinal),
            out _));
    }
}
