using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Calls all registered <see cref="IMetadataProvider"/> implementations concurrently
/// using Task.WhenAll, persists each result as a <c>MetadataLookup</c>
/// row, and writes a <c>ProviderLookup</c> audit event per call (FR-META-002,
/// CTRL-OGMA-018). A failing provider yields a zero-confidence result rather than
/// an exception.
/// </summary>
public sealed class MetadataProviderAggregator : IMetadataProviderAggregator
{
    private readonly IReadOnlyList<IMetadataProvider> _providers;
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataProviderAggregator"/>.
    /// </summary>
    /// <param name="providers">All registered metadata providers.</param>
    /// <param name="context">The catalogue DB context for persisting lookup rows.</param>
    public MetadataProviderAggregator(
        IEnumerable<IMetadataProvider> providers,
        CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(context);
        _providers = providers.ToList();
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderMetadataResult>> AggregateAsync(
        string bookId,
        string isbn13,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        // Call all providers concurrently; isolate per-provider failures.
        var tasks = _providers.Select(p => SafeLookupAsync(p, isbn13, cancellationToken)).ToArray();
        ProviderMetadataResult?[] rawResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        var results = new List<ProviderMetadataResult>();

        foreach (ProviderMetadataResult? result in rawResults)
        {
            if (result is null)
            {
                continue;
            }

            results.Add(result);

            // Persist the lookup row.
            _context.MetadataLookups.Add(new MetadataLookupRow
            {
                BookId = bookId,
                Provider = result.Provider,
                RequestIsbn = result.RequestIsbn,
                ResponseJson = result.RawJson,
                Timestamp = result.RetrievedUtc,
                Confidence = result.Confidence,
                Applied = false,
            });

            // Audit event.
            _context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "ProviderLookup",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    provider = result.Provider,
                    isbn = result.RequestIsbn,
                    confidence = result.Confidence,
                }),
                Timestamp = result.RetrievedUtc,
                IsLocalOnly = true,
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return results;
    }

    private static async Task<ProviderMetadataResult?> SafeLookupAsync(
        IMetadataProvider provider,
        string isbn13,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.LookupAsync(isbn13, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Return a zero-confidence placeholder so the caller knows the provider was attempted.
            return new ProviderMetadataResult(
                Provider: provider.ProviderName,
                RequestIsbn: isbn13,
                Title: null,
                Authors: [],
                Publisher: null,
                Year: null,
                Description: null,
                CoverUrl: null,
                Categories: [],
                IsbnNormalized: isbn13,
                Confidence: 0.0,
                RetrievedUtc: DateTimeOffset.UtcNow,
                RawJson: JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }
}
