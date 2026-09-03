namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// Deterministic provider lookup request. ISBN is preferred when available because
/// it is an exact identifier; title and author are used as a fallback when no valid
/// ISBN can be detected from the file or catalogue.
/// </summary>
/// <param name="Isbn13">The normalized ISBN-13/ISBN-10 value, digits only.</param>
/// <param name="Title">The best known title candidate, if any.</param>
/// <param name="Author">The best known primary author candidate, if any.</param>
/// <param name="ConditionalETag">Previously observed provider ETag, if any.</param>
public sealed record MetadataLookupRequest(
    string? Isbn13,
    string? Title,
    string? Author,
    string? ConditionalETag = null)
{
    /// <summary>Returns true when the request has enough data for a provider search.</summary>
    public bool HasAnySearchKey =>
        !string.IsNullOrWhiteSpace(Isbn13) ||
        !string.IsNullOrWhiteSpace(Title) ||
        !string.IsNullOrWhiteSpace(Author);
}

/// <summary>
/// The raw metadata result returned by a single external provider lookup (FR-META-002).
/// All fields are optional strings since any provider may omit any field.
/// </summary>
/// <param name="Provider">The provider name (e.g. "GoogleBooks", "OpenLibrary").</param>
/// <param name="RequestIsbn">The ISBN-13 used to request this data.</param>
/// <param name="Title">The bibliographic title returned by the provider, if any.</param>
/// <param name="Authors">Author display names returned by the provider.</param>
/// <param name="Publisher">Publisher name, if any.</param>
/// <param name="Year">Publication year, if available.</param>
/// <param name="Description">Book description or synopsis, if any.</param>
/// <param name="CoverUrl">URL of the cover image, if provided.</param>
/// <param name="Categories">Subject categories or tags, if any.</param>
/// <param name="IsbnNormalized">Canonical ISBN-13 echoed back by the provider, if any.</param>
/// <param name="Confidence">Initial match confidence score in [0.0, 1.0] (refined by merge).</param>
/// <param name="RetrievedUtc">UTC timestamp when this result was fetched.</param>
/// <param name="RawJson">The raw JSON response from the provider, for audit storage.</param>
/// <param name="AverageRating">Provider average rating, if supplied.</param>
/// <param name="RatingsCount">Provider rating count, if supplied.</param>
/// <param name="PageCount">Provider page count, if supplied.</param>
/// <param name="Language">Provider language code, if supplied.</param>
/// <param name="IsStale">Whether this result came from an expired local cache.</param>
/// <param name="ETag">Provider validator for conditional revalidation, if supplied.</param>
/// <param name="NotModified">Whether the provider confirmed the cached representation.</param>
public sealed record ProviderMetadataResult(
    string Provider,
    string RequestIsbn,
    string? Title,
    IReadOnlyList<string> Authors,
    string? Publisher,
    int? Year,
    string? Description,
    string? CoverUrl,
    IReadOnlyList<string> Categories,
    string? IsbnNormalized,
    double Confidence,
    DateTimeOffset RetrievedUtc,
    string? RawJson,
    double? AverageRating = null,
    int? RatingsCount = null,
    int? PageCount = null,
    string? Language = null,
    bool IsStale = false,
    string? ETag = null,
    bool NotModified = false);

/// <summary>
/// A single metadata provider that resolves bibliographic data for a given ISBN
/// (FR-META-002). All network I/O must occur in Infrastructure implementations only;
/// the Application layer owns only this contract.
/// </summary>
public interface IMetadataProvider
{
    /// <summary>The stable name identifier for this provider (e.g. "GoogleBooks").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Looks up bibliographic metadata for the supplied ISBN-13.
    /// Returns <see langword="null"/> when the provider finds no match for the ISBN.
    /// </summary>
    /// <param name="isbn13">The normalized 13-digit ISBN (digits only, no hyphens).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ProviderMetadataResult"/> on success; <see langword="null"/> on
    /// not-found. Never throws on network errors — returns a result with
    /// <c>Confidence = 0.0</c> and empty fields.
    /// </returns>
    Task<ProviderMetadataResult?> LookupAsync(
        string isbn13,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for bibliographic metadata using the richest deterministic request
    /// available. Implementations must prefer exact ISBN lookup when
    /// <see cref="MetadataLookupRequest.Isbn13"/> is present and fall back to
    /// provider-native title/author search otherwise. This method never uses AI.
    /// </summary>
    /// <param name="request">The provider search request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Zero or more provider results, ordered by provider relevance.</returns>
    async Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
        MetadataLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.Isbn13))
        {
            ProviderMetadataResult? result = await LookupAsync(request.Isbn13, cancellationToken)
                .ConfigureAwait(false);
            return result is null ? [] : [result];
        }

        return [];
    }
}

/// <summary>
/// Aggregates multiple <see cref="IMetadataProvider"/> instances, calls them
/// concurrently for a given ISBN, persists each result as a <c>MetadataLookup</c> row,
/// and returns all results (FR-META-002). Provider failures are isolated: a failing
/// provider yields a partial result rather than an exception.
/// </summary>
public interface IMetadataProviderAggregator
{
    /// <summary>
    /// Concurrently calls all registered providers for <paramref name="isbn13"/>,
    /// persists each result to <c>MetadataLookups</c>, and writes an
    /// <c>AuditEvent(ProviderLookup)</c> per call.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="isbn13">The normalized ISBN-13 to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// All results from all providers. A provider that fails returns a result with
    /// <c>Confidence = 0.0</c> but is still included so callers know it was attempted.
    /// </returns>
    Task<IReadOnlyList<ProviderMetadataResult>> AggregateAsync(
        string bookId,
        string isbn13,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Concurrently calls all registered providers with a deterministic request that
    /// can contain ISBN, title, and author search keys. Every provider result is
    /// persisted with provenance and audit metadata.
    /// </summary>
    /// <param name="bookId">The catalogue book identifier.</param>
    /// <param name="request">The lookup request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>All provider results, including zero-confidence failure results.</returns>
    Task<IReadOnlyList<ProviderMetadataResult>> AggregateAsync(
        string bookId,
        MetadataLookupRequest request,
        CancellationToken cancellationToken = default);
}
