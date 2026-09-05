using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>Metadata-only recommendation query for the AI advisor.</summary>
public sealed record RecommendationQuery
{
    /// <summary>Creates a recommendation query.</summary>
    /// <param name="queryText">The user's recommendation request.</param>
    /// <param name="maxResults">Maximum recommendation cards to return.</param>
    /// <param name="excludeAlreadyRead">Whether finished books should be excluded.</param>
    /// <param name="shelfFilter">Optional shelf identifier to constrain candidates.</param>
    public RecommendationQuery(
        string queryText,
        int maxResults = 5,
        bool excludeAlreadyRead = false,
        string? shelfFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        if (maxResults is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "Recommendation result count must be between 1 and 25.");
        }

        QueryText = queryText;
        MaxResults = maxResults;
        ExcludeAlreadyRead = excludeAlreadyRead;
        ShelfFilter = shelfFilter;
        Intent = AdvisorIntentParser.Parse(queryText);
    }

    /// <summary>The user's recommendation request.</summary>
    public string QueryText { get; }

    /// <summary>Maximum recommendation cards to return.</summary>
    public int MaxResults { get; }

    /// <summary>Whether finished books should be excluded.</summary>
    public bool ExcludeAlreadyRead { get; }

    /// <summary>Optional shelf identifier to constrain candidates.</summary>
    public string? ShelfFilter { get; }

    /// <summary>Deterministic, editable intent extracted from <see cref="QueryText"/>.</summary>
    public AdvisorIntent Intent { get; }
}

/// <summary>Local catalogue metadata eligible for Tier-1 advisor prompts.</summary>
public sealed record BookMetadataDto(
    string BookId,
    string? Title,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    string? Description,
    string? Notes,
    int? Year,
    IReadOnlyList<string> ShelfIds,
    double? ReadingProgressPct,
    int? PageCount = null);

/// <summary>Token-bounded metadata payload sent through the AI gateway.</summary>
/// <param name="Candidates">Candidate books included in the payload.</param>
/// <param name="MetadataFields">Provider-neutral metadata fields for the gateway.</param>
/// <param name="EstimatedCharacters">Estimated character count of metadata fields.</param>
public sealed record MetadataPayload(
    IReadOnlyList<BookMetadataDto> Candidates,
    IReadOnlyDictionary<string, string> MetadataFields,
    int EstimatedCharacters);

/// <summary>Advisor recommendation feature options.</summary>
public sealed record AdvisorOptions
{
    /// <summary>Creates advisor recommendation options.</summary>
    /// <param name="useHybridRanking">Whether to merge Phase 11 ranking signals into AI recommendations.</param>
    /// <param name="aiWeight">Weight assigned to the provider recommendation order.</param>
    /// <param name="semanticWeight">Weight assigned to Phase 11 semantic/hybrid score.</param>
    public AdvisorOptions(
        bool useHybridRanking = false,
        double aiWeight = 0.6,
        double semanticWeight = 0.4)
    {
        if (!double.IsFinite(aiWeight) || aiWeight < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(aiWeight), aiWeight, "AI recommendation weight cannot be negative.");
        }

        if (!double.IsFinite(semanticWeight) || semanticWeight < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(semanticWeight), semanticWeight, "Semantic recommendation weight cannot be negative.");
        }

        UseHybridRanking = useHybridRanking;
        AiWeight = aiWeight;
        SemanticWeight = semanticWeight;
    }

    /// <summary>Default advisor options; hybrid ranking is disabled until performance is proven.</summary>
    public static AdvisorOptions Default { get; } = new();

    /// <summary>Whether to merge Phase 11 ranking signals into AI recommendations.</summary>
    public bool UseHybridRanking { get; }

    /// <summary>Weight assigned to the provider recommendation order.</summary>
    public double AiWeight { get; }

    /// <summary>Weight assigned to Phase 11 semantic/hybrid score.</summary>
    public double SemanticWeight { get; }
}

/// <summary>Provider/model/tier settings for a recommendation gateway call.</summary>
public sealed record RecommendationGenerationOptions
{
    /// <summary>Creates recommendation generation options.</summary>
    /// <param name="tier">Requested AI privacy tier.</param>
    /// <param name="provider">Provider key, for example openai, anthropic, or ollama.</param>
    /// <param name="model">Provider model identifier.</param>
    /// <param name="advisorOptions">Advisor feature options.</param>
    public RecommendationGenerationOptions(
        AiPrivacyTier tier,
        string provider,
        string model,
        AdvisorOptions? advisorOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        Tier = tier;
        Provider = provider;
        Model = model;
        AdvisorOptions = advisorOptions ?? AdvisorOptions.Default;
    }

    /// <summary>Requested AI privacy tier.</summary>
    public AiPrivacyTier Tier { get; }

    /// <summary>Provider key, for example openai, anthropic, or ollama.</summary>
    public string Provider { get; }

    /// <summary>Provider model identifier.</summary>
    public string Model { get; }

    /// <summary>Advisor feature options.</summary>
    public AdvisorOptions AdvisorOptions { get; }
}

/// <summary>Reads local catalogue metadata for recommendation candidates.</summary>
public interface IAdvisorCatalogueReader
{
    /// <summary>Returns local candidate books for the supplied recommendation query.</summary>
    Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken);
}

/// <summary>Builds a Tier-1 metadata payload from local catalogue candidates.</summary>
public interface IMetadataPayloadEnricher
{
    /// <summary>Builds a bounded metadata payload.</summary>
    MetadataPayload BuildPayload(IReadOnlyList<BookMetadataDto> candidates);
}

/// <summary>Parses provider recommendation JSON into structural domain cards.</summary>
public interface IRecommendationResponseParser
{
    /// <summary>Parses provider output into recommendation cards.</summary>
    IReadOnlyList<RecommendationCard> Parse(
        string responseText,
        string modelUsed,
        AiPrivacyTier tier);
}

/// <summary>Validates that recommendation cards cite only local catalogue provenance.</summary>
public interface IRecommendationProvenanceValidator
{
    /// <summary>Removes hallucinated identifiers or returns a deterministic local fallback ranking.</summary>
    IReadOnlyList<RecommendationCard> Validate(
        IReadOnlyList<RecommendationCard> cards,
        IReadOnlyList<BookMetadataDto> localCandidates,
        int maxResults,
        string modelUsed,
        AiPrivacyTier tier);
}

/// <summary>Structural validation result for AI advisor recommendations.</summary>
/// <param name="IsValid">Whether the structural oracle passed.</param>
/// <param name="Errors">Validation errors, if any.</param>
public sealed record AdvisorValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>A successful validation result.</summary>
    public static AdvisorValidationResult Success { get; } = new(true, []);
}

/// <summary>Deterministic structural oracle for recommendation cards.</summary>
public interface IRecommendationStructuralValidator
{
    /// <summary>Validates card shape, confidence bounds, provenance, and sequential rank.</summary>
    AdvisorValidationResult Validate(IReadOnlyList<RecommendationCard> cards);
}

/// <summary>Candidate enriched with Phase 11 semantic/hybrid ranking signals.</summary>
/// <param name="Candidate">The local metadata candidate.</param>
/// <param name="HybridScore">Normalized Phase 11 hybrid score in [0,1].</param>
/// <param name="SemanticScore">Optional normalized semantic score in [0,1].</param>
public sealed record RankedCandidate(
    BookMetadataDto Candidate,
    double HybridScore,
    double? SemanticScore);

/// <summary>Reading-plan generation request.</summary>
public sealed record ReadingPlanRequest
{
    /// <summary>Creates a reading-plan request.</summary>
    /// <param name="goal">The user's learning or reading objective.</param>
    /// <param name="maxBooks">Maximum books to include in the plan.</param>
    /// <param name="difficultyPreference">Optional preferred difficulty level.</param>
    /// <param name="shelfFilter">Optional shelf identifier to constrain candidates.</param>
    /// <param name="seedBookIds">Optional seed books to prioritize.</param>
    public ReadingPlanRequest(
        string goal,
        int maxBooks = 10,
        DifficultyLabel? difficultyPreference = null,
        string? shelfFilter = null,
        IReadOnlyList<string>? seedBookIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        if (maxBooks is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBooks), maxBooks, "Reading plan book count must be between 1 and 25.");
        }

        Goal = goal;
        MaxBooks = maxBooks;
        DifficultyPreference = difficultyPreference;
        ShelfFilter = shelfFilter;
        SeedBookIds = seedBookIds ?? [];
    }

    /// <summary>The user's learning or reading objective.</summary>
    public string Goal { get; }

    /// <summary>Maximum books to include in the plan.</summary>
    public int MaxBooks { get; }

    /// <summary>Optional preferred difficulty level.</summary>
    public DifficultyLabel? DifficultyPreference { get; }

    /// <summary>Optional shelf identifier to constrain candidates.</summary>
    public string? ShelfFilter { get; }

    /// <summary>Optional seed books to prioritize.</summary>
    public IReadOnlyList<string> SeedBookIds { get; }
}

/// <summary>Answer-mode request for the V2 local-evidence implementation.</summary>
public sealed record AnswerRequest
{
    /// <summary>Creates an answer request.</summary>
    /// <param name="question">The user's local-evidence question.</param>
    /// <param name="maxCitations">Maximum citations to return.</param>
    /// <param name="allowContentAwareTier">Whether V2 may use content-aware local evidence.</param>
    public AnswerRequest(string question, int maxCitations = 5, bool allowContentAwareTier = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        if (maxCitations is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCitations), maxCitations, "Answer citation count must be between 1 and 25.");
        }

        Question = question;
        MaxCitations = maxCitations;
        AllowContentAwareTier = allowContentAwareTier;
    }

    /// <summary>The user's local-evidence question.</summary>
    public string Question { get; }

    /// <summary>Maximum citations to return.</summary>
    public int MaxCitations { get; }

    /// <summary>Whether V2 may use content-aware local evidence.</summary>
    public bool AllowContentAwareTier { get; }
}

/// <summary>Answer-mode response from the V2 local-evidence implementation.</summary>
/// <param name="Answer">Generated answer text.</param>
/// <param name="Citations">Local evidence citations.</param>
/// <param name="IsV2">Whether the full V2 answer mode produced this response.</param>
public sealed record AnswerResponse(
    string Answer,
    IReadOnlyList<AnswerCitation> Citations,
    bool IsV2);

/// <summary>Consumes Phase 11 ranking without leaking search implementation details into the advisor pipeline.</summary>
public interface IHybridRankerConsumer
{
    /// <summary>Ranks local recommendation candidates with Phase 11 signals.</summary>
    Task<IReadOnlyList<RankedCandidate>> RankAsync(
        RecommendationQuery query,
        IReadOnlyList<BookMetadataDto> candidates,
        CancellationToken cancellationToken);
}

/// <summary>Merges provider recommendation order with Phase 11 semantic/hybrid ranking signals.</summary>
public interface IHybridRecommendationMerger
{
    /// <summary>Returns a re-ranked recommendation list.</summary>
    IReadOnlyList<RecommendationCard> Merge(
        IReadOnlyList<RecommendationCard> aiCards,
        IReadOnlyList<RankedCandidate> rankedCandidates,
        AdvisorOptions options,
        int maxResults);
}

/// <summary>Parses provider reading-plan JSON into the structural domain plan.</summary>
public interface IReadingPlanParser
{
    /// <summary>Parses and validates a reading-plan provider response.</summary>
    ReadingPlan Parse(
        string responseText,
        IReadOnlyList<BookMetadataDto> localCandidates);
}

/// <summary>Gateway-backed reading-plan generation pipeline.</summary>
public interface IReadingPlanPipeline
{
    /// <summary>Generates a structurally validated reading plan.</summary>
    Task<ReadingPlan> GetReadingPlanAsync(
        ReadingPlanRequest request,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken);
}

/// <summary>Metadata-only recommendation pipeline.</summary>
public interface IRecommendationPipeline
{
    /// <summary>Generates structurally validated recommendation cards.</summary>
    Task<IReadOnlyList<RecommendationCard>> GetRecommendationsAsync(
        RecommendationQuery query,
        RecommendationGenerationOptions options,
        CancellationToken cancellationToken);
}

/// <summary>Thrown when an advisor provider response cannot be parsed or structurally validated.</summary>
public sealed class AdvisorParseException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="AdvisorParseException"/>.</summary>
    public AdvisorParseException(string message)
        : base(message)
    {
    }
}
