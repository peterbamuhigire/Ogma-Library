using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
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
    private readonly IAuditRepository? _audit;

    /// <summary>Initializes a new instance of <see cref="RecommendationPipeline"/>.</summary>
    public RecommendationPipeline(
        IAdvisorCatalogueReader catalogueReader,
        IMetadataPayloadEnricher payloadEnricher,
        IAiGateway gateway,
        IRecommendationResponseParser parser,
        IRecommendationProvenanceValidator provenanceValidator,
        IRecommendationStructuralValidator structuralValidator,
        IHybridRankerConsumer hybridRanker,
        IHybridRecommendationMerger hybridMerger,
        IAuditRepository? audit = null)
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
        _audit = audit;
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
            await RecordTraceAsync(query, options, candidates, [], "empty-candidate-set", cancellationToken).ConfigureAwait(false);
            return [];
        }

        MetadataPayload payload = _payloadEnricher.BuildPayload(candidates);
        Dictionary<string, string> fields = new(payload.MetadataFields, StringComparer.Ordinal)
        {
            ["prompt.template"] = RecommendationPromptTemplate.Load(),
            ["query.max_results"] = query.MaxResults.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["intent.version"] = query.Intent.Version,
            ["intent.positive_terms"] = string.Join(", ", query.Intent.PositiveTerms),
            ["intent.negative_terms"] = string.Join(", ", query.Intent.NegativeTerms),
            ["intent.mood"] = string.Join(", ", query.Intent.MoodTerms),
            ["intent.difficulty"] = query.Intent.Difficulty?.ToString() ?? "any",
            ["intent.length"] = query.Intent.Length.ToString(),
            ["intent.comparison_reference"] = query.Intent.ComparisonReference ?? string.Empty,
            ["intent.broad_discovery"] = query.Intent.IsBroadDiscovery.ToString(),
        };

        AiRequest request = new(
            options.Tier,
            options.Provider,
            options.Model,
            "recommendation",
            query.QueryText,
            fields);

        AiCompletion completion;
        try
        {
            completion = await _gateway.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AiDisabledException)
        {
            IReadOnlyList<RecommendationCard> fallback = DeterministicAdvisorFallback.Build(candidates, query.Intent, query.MaxResults, options.Tier);
            await RecordTraceAsync(query, options, candidates, fallback, "deterministic-fallback-disabled", cancellationToken).ConfigureAwait(false);
            return fallback;
        }
        catch (AiTierViolationException)
        {
            // A disabled/mismatched provider is a local availability condition;
            // the catalogue remains safe to rank without hiding explicit preview
            // cancellation or missing cloud consent.
            IReadOnlyList<RecommendationCard> fallback = DeterministicAdvisorFallback.Build(candidates, query.Intent, query.MaxResults, options.Tier);
            await RecordTraceAsync(query, options, candidates, fallback, "deterministic-fallback-tier", cancellationToken).ConfigureAwait(false);
            return fallback;
        }
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

        await RecordTraceAsync(query, options, candidates, localOnly, "provider-success", cancellationToken).ConfigureAwait(false);
        return localOnly;
    }

    private async Task RecordTraceAsync(
        RecommendationQuery query,
        RecommendationGenerationOptions options,
        IReadOnlyList<BookMetadataDto> candidates,
        IReadOnlyList<RecommendationCard> results,
        string outcome,
        CancellationToken cancellationToken)
    {
        if (_audit is null)
        {
            return;
        }

        var intent = new
        {
            version = query.Intent.Version,
            positiveTerms = query.Intent.PositiveTerms,
            negativeTerms = query.Intent.NegativeTerms,
            moodTerms = query.Intent.MoodTerms,
            difficulty = query.Intent.Difficulty?.ToString(),
            length = query.Intent.Length.ToString(),
            comparisonReference = query.Intent.ComparisonReference,
            broadDiscovery = query.Intent.IsBroadDiscovery,
        };
        var trace = new
        {
            traceVersion = "advisor-trace-v1",
            queryHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(query.QueryText))),
            intent,
            candidateCount = candidates.Count,
            candidateBookIds = candidates.Take(50).Select(candidate => candidate.BookId).ToArray(),
            resultBookIds = results.Take(query.MaxResults).Select(result => result.BookId.Value).ToArray(),
            outcome,
            provider = options.Provider,
            model = options.Model,
        };

        await _audit.AppendAsync(
            new AuditEvent
            {
                Id = $"advisor-trace-{Guid.NewGuid():N}",
                EventType = "AdvisorExecutionTrace",
                TimestampUtc = DateTimeOffset.UtcNow,
                Payload = JsonSerializer.Serialize(trace),
            },
            cancellationToken).ConfigureAwait(false);
    }
}
