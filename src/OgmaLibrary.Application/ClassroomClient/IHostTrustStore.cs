namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Stores trusted Host certificate fingerprints for Client-mode TOFU.</summary>
public interface IHostTrustStore
{
    /// <summary>Gets a pinned Host fingerprint by stable Host key, or <see langword="null" />.</summary>
    Task<HostTrustPin?> GetAsync(string hostKey, CancellationToken cancellationToken = default);

    /// <summary>Saves or replaces a trusted Host fingerprint.</summary>
    Task SaveAsync(HostTrustPin pin, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Host trust pin.</summary>
    Task DeleteAsync(string hostKey, CancellationToken cancellationToken = default);
}
