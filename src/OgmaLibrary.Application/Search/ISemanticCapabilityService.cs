namespace OgmaLibrary.Application.Search;

/// <summary>Which embedding provider semantic search uses (Sept-23 Phase 14, D-103).</summary>
public enum SemanticProviderChoice
{
    /// <summary>The built-in local ONNX model (default; needs a one-time consented download).</summary>
    BuiltIn = 0,

    /// <summary>A local Ollama service (advanced).</summary>
    Ollama = 1,

    /// <summary>Semantic search is switched off; keyword search keeps working.</summary>
    Off = 2,
}

/// <summary>
/// The single answer to "can semantic search run, and with what?" Consumers
/// read <see cref="IsAvailable"/> instead of probing a provider on every call
/// and re-read it when <see cref="Changed"/> fires: on a provider choice
/// change, on model install or removal, and on the periodic refresh.
/// </summary>
public interface ISemanticCapabilityService
{
    /// <summary>The effective provider choice (environment override, otherwise the user's preference).</summary>
    SemanticProviderChoice Choice { get; }

    /// <summary>True when <c>OGMA_SEMANTIC_PROVIDER</c> fixes the choice, so Settings shows it disabled.</summary>
    bool IsManagedByEnvironment { get; }

    /// <summary>The last known availability. <see cref="SemanticProviderChoice.Off"/> is always unavailable.</summary>
    bool IsAvailable { get; }

    /// <summary>Raised once whenever <see cref="Choice"/> or <see cref="IsAvailable"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Applies the user's persisted provider choice; ignored while an environment override is active.</summary>
    /// <param name="preferences">The current preferences.</param>
    void ApplyPreferences(UserPreferences preferences);

    /// <summary>Re-probes the chosen provider without any network call to a remote host.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The refreshed availability.</returns>
    Task<bool> RefreshAsync(CancellationToken cancellationToken);
}
