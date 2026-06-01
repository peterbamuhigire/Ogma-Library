using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Gateway-backed metadata-only recommendation pipeline.</summary>
public sealed class RecommendationPipeline : IRecommendationPipeline
{
    private readonly IAdvisorCatalogueReader _catalogueReader;
    private readonly IMetadataPayloadEnricher _payloadEnricher;
    private readonly IAiGateway _gateway;
    private readonly IRecommendationResponseParser _parser;
    private readonly IRecommendationProvenanceValidator _provenanceValidator;
    private readonly IRecommendationStructuralValidator _structuralValidator;
    private readonly IHybridRankerConsumer _hybridRanker;
    private readonly IHybridRecommendationMerger _hybridMerger;

    /// <summary>Initializes a new instance of <see cref="RecommendationPipeline"/>.</summary>
    public RecommendationPipeline(
        IAdvisorCatalogueReader catalogueReader,
        IMetadataPayloadEnricher payloadEnricher,
        IAiGateway gateway,
        IRecommendationResponseParser parser,
        IRecommendationProvenanceValidator provenanceValidator,
        IRecommendationStructuralValidator structuralValidator,
        IHybridRankerConsumer hybridRanker,
        IHybridRecommendationMerger hybridMerger)
    {
        ArgumentNullException.ThrowIfNull(catalogueReader);
        ArgumentNullException.ThrowIfNull(payloadEnricher);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(provenanceValidator);
        ArgumentNullException.ThrowIfNull(structuralValidator);
        ArgumentNullException.ThrowIfNull(hybridRanker);
        ArgumentNullException.ThrowIfNull(hybridMerger);

        _catalogueReader = catalogueReader;
        _payloadEnricher = payloadEnricher;
        _gateway = gateway;
        _parser = parser;
        _provenanceValidator = provenanceValidator;
        _structuralValidator = structuralValidator;
        _hybridRanker = hybridRanker;
        _hybridMerger = hybridMerger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
        RecommendationQuery query,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<BookMetadataDto> candidates = await _catalogueReader.GetCandidatesAsync(query, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return [];
        }

        MetadataPayload payload = _payloadEnricher.BuildPayload(candidates);
        Dictionary<string, string> fields = new(payload.MetadataFields, StringComparer.Ordinal)
        {
            ["prompt.template"] = RecommendationPromptTemplate.Load(),
            ["query.max_results"] = query.MaxResults.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        AiRequest request = new(
            options.Tier,
            options.Provider,
            options.Model,
            "recommendation",
            query.QueryText,
            fields);

        AiCompletion completion = await _gateway.SendAsync(request, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RecommendationCard> parsed = _parser.Parse(completion.Text, options.Model, options.Tier);
        IReadOnlyList<RecommendationCard> localOnly = _provenanceValidator.Validate(
            parsed,
            payload.Candidates,
            query.MaxResults,
            options.Model,
            options.Tier);
        if (options.AdvisorOptions.UseHybridRanking)
        {
            IReadOnlyList<RankedCandidate> rankedCandidates = await _hybridRanker
                .RankAsync(query, payload.Candidates, cancellationToken)
                .ConfigureAwait(false);
            localOnly = _hybridMerger.Merge(localOnly, rankedCandidates, options.AdvisorOptions, query.MaxResults);
        }

        AdvisorValidationResult validation = _structuralValidator.Validate(localOnly);
        if (!validation.IsValid)
        {
            throw new AdvisorParseException(string.Join("; ", validation.Errors));
        }

        return localOnly;
    }
}
