namespace OgmaLibrary.Application.Ai;

/// <summary>
/// Minimal Phase 11 AI provider contract. Phase 12 expands this into the full
/// provider-neutral gateway while preserving the local embedding egress boundary.
/// </summary>
public interface IAiProvider
{
    /// <summary>Stable provider key used for audit and routing.</summary>
    string ProviderKey { get; }

    /// <summary>True when the provider runs on this device and sends no bytes to cloud hosts.</summary>
    bool IsLocalOnly { get; }
}
