using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Default local catalogue reader for metadata-only recommendations.</summary>
public sealed class AdvisorCatalogueReader : IAdvisorCatalogueReader
{
    private const int CandidateLimit = 50;

    private readonly ICatalogueReadModel _catalogue;
    private readonly IMetadataSearchService _metadataSearch;

    /// <summary>Initializes a new instance of <see cref="AdvisorCatalogueReader"/>.</summary>
    public AdvisorCatalogueReader(ICatalogueReadModel catalogue, IMetadataSearchService metadataSearch)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(metadataSearch);

        _catalogue = catalogue;
        _metadataSearch = metadataSearch;
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

        return candidates
            .Where(candidate => !query.ExcludeAlreadyRead || candidate.ReadingProgressPct is not >= 99.5)
            .Take(CandidateLimit)
            .ToArray();
    }

    private async Task<List<BookMetadataDto>> GetSearchCandidatesAsync(
        RecommendationQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MetadataSearchResult> results = await _metadataSearch
            .SearchAsync(query.QueryText, cancellationToken)
            .ConfigureAwait(false);

        List<BookMetadataDto> candidates = [];
        foreach (MetadataSearchResult result in results.Take(CandidateLimit))
        {
            BookMetadataDto? candidate = await TryReadDetailAsync(result.BookId, [], cancellationToken).ConfigureAwait(false);
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

        string[] tags = ReadListField(detail.MetadataFields, "Tags", "Tag");
        string[] categories = ReadListField(detail.MetadataFields, "Categories", "Category", "Subject", "Subjects");
        string? description = ReadFirstField(detail.MetadataFields, "Description", "Summary", "Abstract");
        string? notes = detail.ReadingMemory?.KeyInsight;

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
            detail.ReadingProgress?.CompletionPct);
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
}
