using OgmaLibrary.Application.Security;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>Validates release descriptor shape and its detached signature.</summary>
public static class ReleaseDescriptorVerifier
{
    /// <summary>
    /// Parses a descriptor and verifies the signature over its exact JSON text.
    /// </summary>
    /// <param name="descriptorJson">The exact descriptor JSON received from the feed.</param>
    /// <param name="signatureBase64">The detached RSA-PSS signature.</param>
    /// <param name="updateVerifier">The verifier bound to the protected public key.</param>
    /// <param name="descriptor">The validated descriptor when verification succeeds.</param>
    /// <returns><see langword="true"/> only when parsing and signature verification both succeed.</returns>
    public static bool TryVerify(
        string descriptorJson,
        string signatureBase64,
        IUpdateVerifier updateVerifier,
        out ReleaseDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(updateVerifier);
        descriptor = null;
        if (!ReleaseDescriptor.TryParse(descriptorJson, out ReleaseDescriptor? parsed) ||
            !updateVerifier.VerifyDescriptor(descriptorJson, signatureBase64))
        {
            return false;
        }

        descriptor = parsed;
        return true;
    }
}
