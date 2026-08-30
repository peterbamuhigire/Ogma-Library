using System.Security.Cryptography;
using System.Text;
using OgmaLibrary.Application.Security;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>AES-256-GCM field encryption with HKDF-SHA256 key derivation.</summary>
public sealed class AesGcmAtRestEncryptionService : IAtRestEncryptionService
{
    private const byte FormatVersion = 1;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] AssociatedData = "OgmaLibrary.AtRest.v1"u8.ToArray();

    /// <inheritdoc />
    public byte[] DeriveKey(byte[] deviceSecret, string scope)
    {
        ArgumentNullException.ThrowIfNull(deviceSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (deviceSecret.Length < KeySize)
        {
            throw new ArgumentException("The device secret must contain at least 256 bits.", nameof(deviceSecret));
        }

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            deviceSecret,
            KeySize,
            Encoding.UTF8.GetBytes(scope.Trim()),
            AssociatedData);
    }

    /// <inheritdoc />
    public string? Protect(string? plaintext, byte[] key, string purpose)
    {
        if (plaintext is null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(plaintext);
        ValidateKeyAndPurpose(key, purpose);

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSize];
        byte[] aad = BuildAssociatedData(purpose);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);

            byte[] envelope = new byte[1 + nonce.Length + tag.Length + ciphertext.Length];
            envelope[0] = FormatVersion;
            nonce.CopyTo(envelope, 1);
            tag.CopyTo(envelope, 1 + nonce.Length);
            ciphertext.CopyTo(envelope, 1 + nonce.Length + tag.Length);
            return "ogma1:" + Convert.ToBase64String(envelope);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public string? Unprotect(string? ciphertext, byte[] key, string purpose)
    {
        if (ciphertext is null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(ciphertext);
        ValidateKeyAndPurpose(key, purpose);
        if (!ciphertext.StartsWith("ogma1:", StringComparison.Ordinal))
        {
            return ciphertext;
        }

        byte[] envelope;
        try
        {
            envelope = Convert.FromBase64String(ciphertext[6..]);
        }
        catch (FormatException error)
        {
            throw new CryptographicException("The encrypted value is malformed.", error);
        }

        if (envelope.Length < 1 + NonceSize + TagSize || envelope[0] != FormatVersion)
        {
            throw new CryptographicException("The encrypted value uses an unsupported format.");
        }

        int ciphertextLength = envelope.Length - 1 - NonceSize - TagSize;
        byte[] nonce = envelope.AsSpan(1, NonceSize).ToArray();
        byte[] tag = envelope.AsSpan(1 + NonceSize, TagSize).ToArray();
        byte[] encrypted = envelope.AsSpan(1 + NonceSize + TagSize, ciphertextLength).ToArray();
        byte[] plaintext = new byte[ciphertextLength];
        byte[] aad = BuildAssociatedData(purpose);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, encrypted, tag, plaintext, aad);
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(envelope);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(encrypted);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private static byte[] BuildAssociatedData(string purpose) =>
        Encoding.UTF8.GetBytes($"OgmaLibrary.AtRest.v1:{purpose.Trim()}");

    private static void ValidateKeyAndPurpose(byte[] key, string purpose)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (key.Length != KeySize)
        {
            throw new ArgumentException("At-rest encryption requires a 256-bit key.", nameof(key));
        }
    }
}
