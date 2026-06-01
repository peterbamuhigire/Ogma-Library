using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Default Phase 12 AI privacy service backed by consent persistence.</summary>
public sealed class AiPrivacyService : IAiPrivacyService
{
    private readonly IAiConsentRepository _consents;
    private readonly IAiPayloadBuilder _payloadBuilder;
    private AiPrivacyTier _activeTier;

    /// <summary>Initializes a new instance of <see cref="AiPrivacyService"/>.</summary>
    public AiPrivacyService(IAiConsentRepository consents, IAiPayloadBuilder payloadBuilder)
    {
        ArgumentNullException.ThrowIfNull(consents);
        ArgumentNullException.ThrowIfNull(payloadBuilder);
        _consents = consents;
        _payloadBuilder = payloadBuilder;
    }

    /// <inheritdoc />
    public AiPrivacyTier GetActiveTier() => _activeTier;

    /// <inheritdoc />
    public Task SetTierAsync(AiPrivacyTier tier, CancellationToken cancellationToken)
    {
        _activeTier = tier;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordConsentAsync(AiConsentRecord consent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consent);
        return _consents.UpsertAsync(consent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasConsentAsync(
        AiPrivacyTier tier,
        string provider,
        string scope,
        CancellationToken cancellationToken)
    {
        AiConsentRecord? consent = await _consents
            .GetActiveConsentAsync(tier, provider, scope, cancellationToken)
            .ConfigureAwait(false);
        return consent?.IsActive is true;
    }

    /// <inheritdoc />
    public AiPayloadPreview BuildPayloadPreview(AiRequest request) =>
        _payloadBuilder.BuildPreview(request);
}
