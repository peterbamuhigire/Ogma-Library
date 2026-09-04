namespace OgmaLibrary.Application.Ai;

/// <summary>Persisted provider configuration without secret material.</summary>
public sealed record AiProviderProfile(
    string ProfileId,
    string ProviderKey,
    string Model,
    Uri? BaseAddress,
    string? CredentialReference,
    bool Enabled,
    bool IsDefault,
    DateTimeOffset UpdatedUtc);

/// <summary>Durable user-managed AI provider profile boundary.</summary>
public interface IAiProviderProfileService
{
    /// <summary>Returns profiles ordered for deterministic settings presentation.</summary>
    Task<IReadOnlyList<AiProviderProfile>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and atomically persists one profile.</summary>
    Task<AiProviderProfile> SaveAsync(
        AiProviderProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a profile without exposing or returning secret material.</summary>
    Task<bool> DeleteAsync(string profileId, CancellationToken cancellationToken = default);
}
