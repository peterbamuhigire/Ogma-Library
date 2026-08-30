namespace OgmaLibrary.Application.Security;

/// <summary>Encrypts sensitive local values before they are persisted at rest.</summary>
public interface IAtRestEncryptionService
{
    /// <summary>Derives a profile-scoped 256-bit key from an OS-protected secret.</summary>
    byte[] DeriveKey(byte[] deviceSecret, string scope);

    /// <summary>Encrypts a value using authenticated encryption.</summary>
    string? Protect(string? plaintext, byte[] key, string purpose);

    /// <summary>Decrypts a value and accepts legacy unencrypted values for migration.</summary>
    string? Unprotect(string? ciphertext, byte[] key, string purpose);
}
