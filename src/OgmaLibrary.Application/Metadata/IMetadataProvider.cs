namespace OgmaLibrary.Application.Metadata;

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
    string? RawJson);

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
}
