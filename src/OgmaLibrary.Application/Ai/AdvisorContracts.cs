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
    }

    /// <summary>The user's recommendation request.</summary>
    public string QueryText { get; }

    /// <summary>Maximum recommendation cards to return.</summary>
    public int MaxResults { get; }

    /// <summary>Whether finished books should be excluded.</summary>
    public bool ExcludeAlreadyRead { get; }

    /// <summary>Optional shelf identifier to constrain candidates.</summary>
    public string? ShelfFilter { get; }
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
    double? ReadingProgressPct);

/// <summary>Token-bounded metadata payload sent through the AI gateway.</summary>
/// <param name="Candidates">Candidate books included in the payload.</param>
/// <param name="MetadataFields">Provider-neutral metadata fields for the gateway.</param>
/// <param name="EstimatedCharacters">Estimated character count of metadata fields.</param>
public sealed record MetadataPayload(
    IReadOnlyList<BookMetadataDto> Candidates,
    IReadOnlyDictionary<string, string> MetadataFields,
    int EstimatedCharacters);

/// <summary>Provider/model/tier settings for a recommendation gateway call.</summary>
public sealed record RecommendationGenerationOptions
{
    /// <summary>Creates recommendation generation options.</summary>
    /// <param name="tier">Requested AI privacy tier.</param>
    /// <param name="provider">Provider key, for example openai, anthropic, or ollama.</param>
    /// <param name="model">Provider model identifier.</param>
    public RecommendationGenerationOptions(AiPrivacyTier tier, string provider, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        Tier = tier;
        Provider = provider;
        Model = model;
    }

    /// <summary>Requested AI privacy tier.</summary>
    public AiPrivacyTier Tier { get; }

    /// <summary>Provider key, for example openai, anthropic, or ollama.</summary>
    public string Provider { get; }

    /// <summary>Provider model identifier.</summary>
    public string Model { get; }
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
