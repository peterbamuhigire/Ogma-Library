using System.Security.Cryptography;
using OgmaLibrary.Infrastructure.Security;

namespace OgmaLibrary.Tests.Security;

/// <summary>Encryption format, authentication, and key-derivation tests.</summary>
public sealed class AesGcmAtRestEncryptionServiceTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsWithProfileDerivedKey()
    {
        var service = new AesGcmAtRestEncryptionService();
        byte[] deviceSecret = RandomNumberGenerator.GetBytes(32);
        byte[] key = service.DeriveKey(deviceSecret, "profile-1");

        string encrypted = service.Protect("A private annotation", key, "annotation")!;

        Assert.StartsWith("ogma1:", encrypted, StringComparison.Ordinal);
        Assert.Equal("A private annotation", service.Unprotect(encrypted, key, "annotation"));
        Assert.NotEqual(key, service.DeriveKey(deviceSecret, "profile-2"));
    }

    [Fact]
    public void Unprotect_RejectsTamperingAndWrongPurpose()
    {
        var service = new AesGcmAtRestEncryptionService();
        byte[] key = service.DeriveKey(RandomNumberGenerator.GetBytes(32), "profile-1");
        string encrypted = service.Protect("private query", key, "query")!;
        char replacement = encrypted[^1] == 'A' ? 'B' : 'A';
        string tampered = encrypted[..^1] + replacement;

        Assert.ThrowsAny<CryptographicException>(() => service.Unprotect(tampered, key, "query"));
        Assert.ThrowsAny<CryptographicException>(() => service.Unprotect(encrypted, key, "response"));
    }

    [Fact]
    public void Unprotect_LegacyPlaintext_RemainsReadableForMigration()
    {
        var service = new AesGcmAtRestEncryptionService();
        byte[] key = service.DeriveKey(RandomNumberGenerator.GetBytes(32), "profile-1");

        Assert.Equal("legacy value", service.Unprotect("legacy value", key, "field"));
        Assert.Null(service.Protect(null, key, "field"));
        Assert.Null(service.Unprotect(null, key, "field"));
    }
}
