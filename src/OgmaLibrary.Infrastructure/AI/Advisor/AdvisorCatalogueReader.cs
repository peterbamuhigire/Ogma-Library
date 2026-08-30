using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Ai.Extensions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Default local catalogue reader for metadata-only recommendations.</summary>
public sealed class AdvisorCatalogueReader : IAdvisorCatalogueReader, IAiCatalogueReader
{
    private const int CandidateLimit = 50;

    private readonly ICatalogueReadModel _catalogue;
    private readonly IMetadataSearchService _metadataSearch;
    private readonly ISemanticSearchService? _semanticSearch;

    /// <summary>Initializes a new instance of <see cref="AdvisorCatalogueReader"/>.</summary>
    public AdvisorCatalogueReader(
        ICatalogueReadModel catalogue,
        IMetadataSearchService metadataSearch,
        ISemanticSearchService? semanticSearch = null)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(metadataSearch);

        _catalogue = catalogue;
        _metadataSearch = metadataSearch;
        _semanticSearch = semanticSearch;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookMetadataDto>> GetCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<BookMetadataDto> candidates = string.IsNullOrWhiteSpace(query.QueryText)
            ? await GetCatalogueCandidatesAsync(query, cancellationToken).ConfigureAwait(false)
            : await GetSearchCandidatesAsync(query, cancellationToken).ConfigureAwait(false);

        return AdvisorCandidateRanker.Rank(
                candidates
            .Where(candidate => !query.ExcludeAlreadyRead || candidate.ReadingProgressPct is not >= 99.5)
            .Where(candidate => MatchesShelf(candidate, query.ShelfFilter))
            .ToArray(),
                query.Intent,
                CandidateLimit)
            .ToArray();
    }

    /// <inheritdoc />
    public Task<BookMetadataDto?> GetByIdAsync(BookId bookId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId.Value);
        return TryReadDetailAsync(bookId.Value, [], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookMetadataDto>> GetByShelfAsync(string shelfId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);

        return await GetCatalogueCandidatesAsync(
            new RecommendationQuery("shelf recommendation source", shelfFilter: shelfId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<BookMetadataDto>> GetSearchCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MetadataSearchResult> results = await _metadataSearch
            .SearchAsync(query.QueryText, cancellationToken)
            .ConfigureAwait(false);

        List<string> metadataIds = results
            .Select(result => result.BookId)
            .Where(bookId => !string.IsNullOrWhiteSpace(bookId))
            .Distinct(StringComparer.Ordinal)
            .Take(CandidateLimit)
            .ToList();
        List<string> semanticIds = [];

        // Metadata search is one signal, not the retrieval gate. Semantic search
        // is attempted for every non-empty request so conceptual matches survive
        // sparse or non-literal metadata.
        if (_semanticSearch is not null)
        {
            SemanticSearchResponse semantic = await _semanticSearch
                .SearchAsync(query.QueryText, CandidateLimit, cancellationToken)
                .ConfigureAwait(false);
            foreach (string bookId in semantic.Results.Select(result => result.BookId))
            {
                if (!string.IsNullOrWhiteSpace(bookId) && !semanticIds.Contains(bookId, StringComparer.Ordinal))
                {
                    semanticIds.Add(bookId);
                }
            }
        }

        // Reserve half of the bounded retrieval window for conceptual results,
        // then backfill from literal metadata results when semantic search is
        // unavailable or returns fewer unique books.
        List<string> candidateIds = metadataIds
            .Take(CandidateLimit / 2)
            .Concat(semanticIds)
            .Concat(metadataIds.Skip(CandidateLimit / 2))
            .Distinct(StringComparer.Ordinal)
            .Take(CandidateLimit)
            .ToList();

        List<BookMetadataDto> candidates = [];
        foreach (string bookId in candidateIds)
        {
            BookMetadataDto? candidate = await TryReadDetailAsync(bookId, [], cancellationToken).ConfigureAwait(false);
            if (candidate is null || !MatchesShelf(candidate, query.ShelfFilter))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    private async Task<List<BookMetadataDto>> GetCatalogueCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken)
    {
        CatalogueFilter filter = new(
            ShelfId: query.ShelfFilter,
            Status: 0,
            MaxResults: CandidateLimit);

        List<BookMetadataDto> candidates = [];
        await foreach (BookSummaryProjection summary in _catalogue.GetBookSummariesAsync(filter, cancellationToken).ConfigureAwait(false))
        {
            BookMetadataDto? candidate = await TryReadDetailAsync(summary.BookId, summary.ShelfIds, cancellationToken).ConfigureAwait(false);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private async Task<BookMetadataDto?> TryReadDetailAsync(
        string bookId,
        IReadOnlyList<string> shelfIds,
        CancellationToken cancellationToken)
    {
        BookDetailProjection? detail = await _catalogue.GetBookDetailAsync(bookId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return null;
        }

        // Search results are revalidated against the authoritative catalogue
        // projection before they can enter the advisor or provider payload.
        if (detail.Status != 0 || !detail.IsAvailable)
        {
            return null;
        }

        string[] tags = ReadListField(detail.MetadataFields, "Tags", "Tag");
        string[] categories = ReadListField(detail.MetadataFields, "Categories", "Category", "Subject", "Subjects");
        string? description = ReadFirstField(detail.MetadataFields, "Description", "Summary", "Abstract");
        string? notes = detail.ReadingMemory?.KeyInsight;
        int? pageCount = ReadIntField(detail.MetadataFields, "Pages", "PageCount", "NumberOfPages");

        return new BookMetadataDto(
            detail.BookId,
            detail.Title,
            detail.Authors,
            tags,
            categories,
            description,
            notes,
            detail.Year,
            shelfIds,
            detail.ReadingProgress?.CompletionPct,
            pageCount);
    }

    private static bool MatchesShelf(BookMetadataDto candidate, string? shelfFilter) =>
        string.IsNullOrWhiteSpace(shelfFilter) ||
        candidate.ShelfIds.Contains(shelfFilter, StringComparer.Ordinal);

    private static string? ReadFirstField(IReadOnlyList<MetadataFieldProjection> fields, params string[] names) =>
        fields
            .Where(field => names.Contains(field.FieldName, StringComparer.OrdinalIgnoreCase))
            .Select(field => field.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string[] ReadListField(IReadOnlyList<MetadataFieldProjection> fields, params string[] names)
    {
        string? value = ReadFirstField(fields, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int? ReadIntField(IReadOnlyList<MetadataFieldProjection> fields, params string[] names)
    {
        string? value = ReadFirstField(fields, names);
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : null;
    }
}
