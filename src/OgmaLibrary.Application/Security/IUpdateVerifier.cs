namespace OgmaLibrary.Application.Security;

/// <summary>Verifies the signed update trust chain before an update is applied.</summary>
public interface IUpdateVerifier
{
    /// <summary>Verifies the detached signature for the exact descriptor JSON.</summary>
    bool VerifyDescriptor(string descriptorJson, string signatureBase64);

    /// <summary>Verifies the SHA-256 digest of a downloaded package.</summary>
    bool VerifyPackage(Stream package, string expectedSha256Hex);
}
