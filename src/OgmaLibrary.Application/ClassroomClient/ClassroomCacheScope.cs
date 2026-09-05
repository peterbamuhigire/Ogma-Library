namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Builds the stable cache identity for one trusted Host certificate.</summary>
public static class ClassroomCacheScope
{
    /// <summary>
    /// Returns the normalized address, port, and certificate fingerprint scope
    /// used by Host-resource caching.
    /// </summary>
    public static string CreateHostId(ClassroomJoinRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return $"{request.Address.Trim().ToLowerInvariant()}:{request.Port}:" +
            request.CertificateFingerprint.Trim().ToLowerInvariant();
    }
}
