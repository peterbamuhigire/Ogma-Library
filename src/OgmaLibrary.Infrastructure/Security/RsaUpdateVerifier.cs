using System.Security.Cryptography;
using OgmaLibrary.Application.Security;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>RSA-4096/SHA-256 implementation of the update trust verifier.</summary>
public sealed class RsaUpdateVerifier : IUpdateVerifier, IDisposable
{
    private readonly RSA _publicKey;

    /// <summary>Initializes a verifier from an RFC 7468 PEM public key.</summary>
    /// <param name="publicKeyPem">The embedded or configuration-provided public key.</param>
    public RsaUpdateVerifier(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        _publicKey = RSA.Create();
        _publicKey.ImportFromPem(publicKeyPem);
        if (_publicKey.KeySize < 3072)
        {
            _publicKey.Dispose();
            throw new ArgumentException("Update signing keys must be at least RSA-3072.", nameof(publicKeyPem));
        }
    }

    /// <inheritdoc />
    public bool VerifyDescriptor(string descriptorJson, string signatureBase64)
    {
        ArgumentNullException.ThrowIfNull(descriptorJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureBase64);

        try
        {
            byte[] signature = Convert.FromBase64String(signatureBase64);
            byte[] content = System.Text.Encoding.UTF8.GetBytes(descriptorJson);
            try
            {
                return _publicKey.VerifyData(content, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(content);
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool VerifyPackage(Stream package, string expectedSha256Hex)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256Hex);
        if (expectedSha256Hex.Length != 64 ||
            expectedSha256Hex.Any(static character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        if (package.CanSeek)
        {
            package.Position = 0;
        }

        byte[] actual = SHA256.HashData(package);
        return CryptographicOperations.FixedTimeEquals(
            actual,
            Convert.FromHexString(expectedSha256Hex));
    }

    /// <inheritdoc />
    public void Dispose() => _publicKey.Dispose();

}
