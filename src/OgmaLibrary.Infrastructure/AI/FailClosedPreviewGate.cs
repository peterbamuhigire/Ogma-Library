using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Default non-UI preview gate that fails closed by cancelling egress.</summary>
public sealed class FailClosedPreviewGate : IAiPreviewGate
{
    /// <inheritdoc />
    public Task<AiPreviewDecision> ShowAsync(
        AiPayloadPreview preview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AiPreviewDecision.Cancel);
    }
}
