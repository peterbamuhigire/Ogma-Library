using System.Security.Cryptography;
using System.Text;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>
/// Central AI gateway that enforces privacy tier, payload preview, consent,
/// provider dispatch, query-history retention, and immutable audit for off-device calls.
/// </summary>
public sealed class AiGateway : IAiGateway
{
    private readonly IAiProvider _provider;
    private readonly IAiPrivacyService _privacy;
    private readonly IAiPayloadBuilder _payloadBuilder;
    private readonly IAiPreviewGate _previewGate;
    private readonly IAiAuditRepository _audit;
    private readonly IAiQueryHistoryRepository _history;
    private readonly IAiCostCalculator _costs;
    private bool _previewRememberedForSession;

    /// <summary>Initializes a new instance of <see cref="AiGateway"/>.</summary>
    public AiGateway(
        IAiProvider provider,
        IAiPrivacyService privacy,
        IAiPayloadBuilder payloadBuilder,
        IAiPreviewGate previewGate,
        IAiAuditRepository audit,
        IAiQueryHistoryRepository history,
        IAiCostCalculator costs)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(privacy);
        ArgumentNullException.ThrowIfNull(payloadBuilder);
        ArgumentNullException.ThrowIfNull(previewGate);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(costs);

        _provider = provider;
        _privacy = privacy;
        _payloadBuilder = payloadBuilder;
        _previewGate = previewGate;
        _audit = audit;
        _history = history;
        _costs = costs;
    }

    /// <inheritdoc />
    public async Task<AiCompletion> SendAsync(AiRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(_provider.ProviderKey, request.Provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new AiTierViolationException("The configured AI provider does not match the requested provider.");
        }
        AiPrivacyTier activeTier = _privacy.GetActiveTier();
        EnsureTierAllowed(activeTier, request);

        bool isLocal = request.Tier == AiPrivacyTier.LocalOllama;
        if (isLocal && !_provider.IsLocalOnly)
        {
            throw new AiTierViolationException("LocalOllama requests require a local-only provider.");
        }

        AiPayloadPreview preview = _payloadBuilder.BuildPreview(request);
        string payloadHash = _payloadBuilder.ComputePayloadHash(preview);

        if (!isLocal)
        {
            await RequirePreviewAsync(preview, cancellationToken).ConfigureAwait(false);
            await RequireConsentAsync(request, cancellationToken).ConfigureAwait(false);
        }

        string historyId = CreateId("aihist");
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        try
        {
            AiCompletion completion = await _provider.CompleteAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (isLocal && !completion.IsLocal)
            {
                throw new AiTierViolationException("A local AI request returned a non-local completion.");
            }

            if (!isLocal)
            {
                await _history.AddAsync(
                    new AiQueryHistoryEntry(
                        historyId,
                        occurredAt,
                        request.QueryType,
                        request.QueryText,
                        Summarize(completion.Text)),
                    cancellationToken)
                    .ConfigureAwait(false);

                await _audit.AppendAsync(
                    new AiAuditEvent(
                        CreateId("aiaudit"),
                        occurredAt,
                        request.Tier,
                        request.Provider,
                        request.Model,
                        payloadHash,
                        HashText(completion.Text),
                        completion.PromptTokens,
                        completion.CompletionTokens,
                        completion.PromptCacheTokens,
                        _costs.EstimateCostUsd(request, completion),
                        historyId),
                    cancellationToken)
                    .ConfigureAwait(false);
            }

            return completion;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!isLocal)
            {
                await _audit.AppendAsync(
                    new AiAuditEvent(
                        CreateId("aiaudit"),
                        occurredAt,
                        request.Tier,
                        request.Provider,
                        request.Model,
                        payloadHash,
                        HashText($"{ex.GetType().FullName}:{ex.Message}")),
                    cancellationToken)
                    .ConfigureAwait(false);
            }

            throw;
        }
    }

    private static void EnsureTierAllowed(AiPrivacyTier activeTier, AiRequest request)
    {
        if (activeTier == AiPrivacyTier.Offline || request.Tier == AiPrivacyTier.Offline)
        {
            throw new AiDisabledException();
        }

        if (request.Tier == AiPrivacyTier.LocalOllama)
        {
            if (activeTier != AiPrivacyTier.LocalOllama)
            {
                throw new AiTierViolationException("Local Ollama requests require the LocalOllama privacy tier.");
            }

            return;
        }

        if (activeTier == AiPrivacyTier.LocalOllama)
        {
            throw new AiTierViolationException("Cloud AI requests are not allowed while LocalOllama is the active tier.");
        }

        if (request.Tier > activeTier)
        {
            throw new AiTierViolationException(
                $"Request tier '{request.Tier}' exceeds active tier '{activeTier}'.");
        }
    }

    private async Task RequirePreviewAsync(AiPayloadPreview preview, CancellationToken cancellationToken)
    {
        if (_previewRememberedForSession)
        {
            return;
        }

        AiPreviewDecision decision = await _previewGate.ShowAsync(preview, cancellationToken)
            .ConfigureAwait(false);
        if (decision == AiPreviewDecision.Cancel)
        {
            throw new AiPreviewCancelledException();
        }

        _previewRememberedForSession = decision == AiPreviewDecision.RememberForSession;
    }

    private async Task RequireConsentAsync(AiRequest request, CancellationToken cancellationToken)
    {
        bool hasConsent = await _privacy.HasConsentAsync(
            request.Tier,
            request.Provider,
            request.ConsentScope,
            cancellationToken)
            .ConfigureAwait(false);
        if (!hasConsent)
        {
            throw new AiConsentRequiredException(request.Tier, request.Provider, request.ConsentScope);
        }
    }

    private static string CreateId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string Summarize(string text)
    {
        const int maxSummaryLength = 512;
        return text.Length <= maxSummaryLength ? text : text[..maxSummaryLength];
    }

    private static string HashText(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(digest);
    }
}
