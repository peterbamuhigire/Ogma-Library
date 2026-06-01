using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Gateway-backed structured reading-plan pipeline.</summary>
public sealed class ReadingPlanPipeline : IReadingPlanPipeline
{
    private readonly IAdvisorCatalogueReader _catalogueReader;
    private readonly IMetadataPayloadEnricher _payloadEnricher;
    private readonly IAiGateway _gateway;
    private readonly IReadingPlanParser _parser;

    /// <summary>Initializes a new instance of <see cref="ReadingPlanPipeline"/>.</summary>
    public ReadingPlanPipeline(
        IAdvisorCatalogueReader catalogueReader,
        IMetadataPayloadEnricher payloadEnricher,
        IAiGateway gateway,
        IReadingPlanParser parser)
    {
        ArgumentNullException.ThrowIfNull(catalogueReader);
        ArgumentNullException.ThrowIfNull(payloadEnricher);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(parser);

        _catalogueReader = catalogueReader;
        _payloadEnricher = payloadEnricher;
        _gateway = gateway;
        _parser = parser;
    }

    /// <inheritdoc />
    public async Task<ReadingPlan> GetReadingPlanAsync(
        ReadingPlanRequest request,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<BookMetadataDto> candidates = await _catalogueReader
            .GetCandidatesAsync(ToRecommendationQuery(request), cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            throw new AdvisorParseException("A reading plan requires at least one local catalogue candidate.");
        }

        MetadataPayload payload = _payloadEnricher.BuildPayload(PrioritizeSeeds(candidates, request.SeedBookIds));
        Dictionary<string, string> fields = new(payload.MetadataFields, StringComparer.Ordinal)
        {
            ["prompt.template"] = ReadingPlanPromptTemplate.Load(),
            ["plan.max_books"] = request.MaxBooks.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (request.DifficultyPreference.HasValue)
        {
            fields["plan.difficulty_preference"] = request.DifficultyPreference.Value.ToString();
        }

        AiRequest aiRequest = new(
            options.Tier,
            options.Provider,
            options.Model,
            "reading-plan",
            request.Goal,
            fields);

        AdvisorParseException? firstFailure = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            AiCompletion completion = await _gateway.SendAsync(aiRequest, cancellationToken).ConfigureAwait(false);
            try
            {
                return _parser.Parse(completion.Text, payload.Candidates);
            }
            catch (AdvisorParseException ex) when (attempt == 0)
            {
                firstFailure = ex;
            }
        }

        throw new AdvisorParseException($"Reading plan response failed validation after retry: {firstFailure?.Message}");
    }

    private static RecommendationQuery ToRecommendationQuery(ReadingPlanRequest request) =>
        new(
            request.Goal,
            Math.Min(25, Math.Max(request.MaxBooks, request.MaxBooks * 2)),
            excludeAlreadyRead: false,
            request.ShelfFilter);

    private static IReadOnlyList<BookMetadataDto> PrioritizeSeeds(
        IReadOnlyList<BookMetadataDto> candidates,
        IReadOnlyList<string> seedBookIds)
    {
        if (seedBookIds.Count == 0)
        {
            return candidates;
        }

        Dictionary<string, int> seedOrder = seedBookIds
            .Select((bookId, index) => new { bookId, index })
            .ToDictionary(item => item.bookId, item => item.index, StringComparer.Ordinal);
        return candidates
            .OrderBy(candidate => seedOrder.GetValueOrDefault(candidate.BookId, int.MaxValue))
            .ThenBy(candidate => candidate.BookId, StringComparer.Ordinal)
            .ToArray();
    }
}
