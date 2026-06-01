namespace OgmaLibrary.Application.Ai;

/// <summary>
/// Provider-neutral AI contract. Phase 12 routes all AI completions through
/// this gateway surface while preserving the local embedding boundary.
/// </summary>
public interface IAiProvider
{
    /// <summary>Stable provider key used for audit and routing.</summary>
    string ProviderKey { get; }

    /// <summary>True when the provider runs on this device and sends no bytes to cloud hosts.</summary>
    bool IsLocalOnly { get; }

    /// <summary>
    /// Completes a provider-neutral AI request.
    /// </summary>
    Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not support chat completions.");
}
