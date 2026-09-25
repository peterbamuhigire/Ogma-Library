namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// Decides, at the moment of each lookup, whether online metadata providers may be called
/// (Sept-23 Phase 08, task 8.3). Provider adapters are always registered; this policy gates the
/// network so the user's Settings choice applies without a restart and the network stays off
/// until they opt in.
/// </summary>
public interface IMetadataProviderPolicy
{
    /// <summary>Whether online provider lookups are allowed right now.</summary>
    bool AreOnlineProvidersEnabled { get; }
}

/// <summary>A fixed <see cref="IMetadataProviderPolicy"/> for composition without Settings (tests, tools).</summary>
/// <param name="AreOnlineProvidersEnabled">The fixed decision.</param>
public sealed record FixedMetadataProviderPolicy(bool AreOnlineProvidersEnabled) : IMetadataProviderPolicy
{
    /// <summary>A policy that never allows online lookups.</summary>
    public static FixedMetadataProviderPolicy Disabled { get; } = new(false);
}
