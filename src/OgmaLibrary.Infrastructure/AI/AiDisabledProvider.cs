using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Fail-closed provider used when AI is disabled.</summary>
public sealed class AiDisabledProvider : IAiProvider
{
    /// <inheritdoc />
    public string ProviderKey => "disabled";

    /// <inheritdoc />
    public bool IsLocalOnly => true;

    /// <inheritdoc />
    public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken) =>
        throw new AiDisabledException();
}
