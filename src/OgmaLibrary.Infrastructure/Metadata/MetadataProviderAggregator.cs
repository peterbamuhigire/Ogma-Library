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
    private readonly IMetadataConflictDetector _conflictDetector;
    private readonly IMetadataProviderGateway? _gateway;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataProviderAggregator"/>.
    /// </summary>
    /// <param name="providers">All registered metadata providers.</param>
    /// <param name="context">The catalogue DB context for persisting lookup rows.</param>
    /// <param name="conflictDetector">Optional field-level conflict detector.</param>
    /// <param name="gateway">Optional cached provider gateway used by runtime composition.</param>
    internal MetadataProviderAggregator(
        IEnumerable<IMetadataProvider> providers,
        CatalogueDbContext context,
        IMetadataConflictDetector? conflictDetector = null,
        IMetadataProviderGateway? gateway = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(context);
        _providers = providers.ToList();
        _context = context;
        _conflictDetector = conflictDetector ?? new MetadataConflictDetector();
        _gateway = gateway;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataProviderAggregator"/>.
    /// </summary>
    /// <param name="providers">All registered metadata providers.</param>
    /// <param name="contextFactory">Factory for catalogue DB contexts.</param>
    /// <param name="conflictDetector">Optional field-level conflict detector.</param>
    /// <param name="gateway">Optional cached provider gateway used by runtime composition.</param>
    public MetadataProviderAggregator(
        IEnumerable<IMetadataProvider> providers,
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IMetadataConflictDetector? conflictDetector = null,
        IMetadataProviderGateway? gateway = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(contextFactory);
        _providers = providers.ToList();
        _contextFactory = contextFactory;
        _conflictDetector = conflictDetector ?? new MetadataConflictDetector();
        _gateway = gateway;
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

        // Runtime composition supplies the cached gateway. The direct provider path
        // remains available to focused tests and legacy callers, while preserving
        // per-provider failure isolation when no gateway is configured.
        IReadOnlyList<ProviderMetadataResult>[] providerResults;
        if (_gateway is not null)
        {
            providerResults = [await _gateway.SearchAsync(request, cancellationToken)
                .ConfigureAwait(false)];
        }
        else
        {
            var tasks = _providers
                .Select(p => SafeSearchAsync(p, request, cancellationToken))
                .ToArray();
            providerResults = await Task.WhenAll(tasks).ConfigureAwait(false);
        }

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

        MetadataConflictReport conflictReport = _conflictDetector.Detect(results);
        if (conflictReport.HasConflicts)
        {
            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "ProviderConflict",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    fields = conflictReport.Conflicts.Select(conflict => new
                    {
                        field = conflict.FieldName,
                        providers = conflict.Candidates
                            .Select(candidate => candidate.Provider)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToArray(),
                        candidateCount = conflict.Candidates.Count,
                    }).ToArray(),
                    // Candidate values are intentionally excluded from this audit payload.
                    containsRawValues = false,
                }),
                Timestamp = DateTimeOffset.UtcNow,
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
