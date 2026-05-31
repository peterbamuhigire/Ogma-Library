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
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataProviderAggregator"/>.
    /// </summary>
    /// <param name="providers">All registered metadata providers.</param>
    /// <param name="context">The catalogue DB context for persisting lookup rows.</param>
    internal MetadataProviderAggregator(
        IEnumerable<IMetadataProvider> providers,
        CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(context);
        _providers = providers.ToList();
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataProviderAggregator"/>.
    /// </summary>
    public MetadataProviderAggregator(
        IEnumerable<IMetadataProvider> providers,
        IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(contextFactory);
        _providers = providers.ToList();
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderMetadataResult>> AggregateAsync(
        string bookId,
        string isbn13,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        return await AggregateAsync(
            bookId,
            new MetadataLookupRequest(isbn13, Title: null, Author: null),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderMetadataResult>> AggregateAsync(
        string bookId,
        MetadataLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasAnySearchKey)
        {
            return [];
        }

        // Call all providers concurrently; isolate per-provider failures.
        var tasks = _providers.Select(p => SafeSearchAsync(p, request, cancellationToken)).ToArray();
        IReadOnlyList<ProviderMetadataResult>[] providerResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var results = new List<ProviderMetadataResult>();

        foreach (ProviderMetadataResult result in providerResults.SelectMany(r => r))
        {
            results.Add(result);

            // Persist the lookup row.
            context.MetadataLookups.Add(new MetadataLookupRow
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
            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "ProviderLookup",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    provider = result.Provider,
                    isbn = result.RequestIsbn,
                    title = request.Title,
                    author = request.Author,
                    confidence = result.Confidence,
                }),
                Timestamp = result.RetrievedUtc,
                IsLocalOnly = true,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return results;
    }

    private static async Task<IReadOnlyList<ProviderMetadataResult>> SafeSearchAsync(
        IMetadataProvider provider,
        MetadataLookupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Return a zero-confidence placeholder so the caller knows the provider was attempted.
            return [new ProviderMetadataResult(
                Provider: provider.ProviderName,
                RequestIsbn: request.Isbn13 ?? string.Empty,
                Title: null,
                Authors: [],
                Publisher: null,
                Year: null,
                Description: null,
                CoverUrl: null,
                Categories: [],
                IsbnNormalized: request.Isbn13,
                Confidence: 0.0,
                RetrievedUtc: DateTimeOffset.UtcNow,
                RawJson: JsonSerializer.Serialize(new { error = ex.Message }))];
        }
    }
}
