using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>Provides TTL-cached, failure-isolated provider lookup results.</summary>
public sealed class MetadataProviderGateway : IMetadataProviderGateway
{
    private const int ContractVersion = 1;
    private static readonly TimeSpan SuccessTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromHours(6);
    private readonly List<IMetadataProvider> _providers;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal MetadataProviderGateway(
        IEnumerable<IMetadataProvider> providers,
        CatalogueDbContext context)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>DI constructor using independent contexts per operation.</summary>
    [ActivatorUtilitiesConstructor]
    public MetadataProviderGateway(
        IEnumerable<IMetadataProvider> providers,
        IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
        MetadataLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.HasAnySearchKey || _providers.Count == 0)
        {
            return [];
        }

        string queryKey = NormalizeQuery(request);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        List<ProviderCacheEntryRow> cached = await context.ProviderCacheEntries
            .Where(row => row.ContractVersion == ContractVersion && row.QueryKey == queryKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var results = new List<ProviderMetadataResult>();
        var staleResults = new Dictionary<string, IReadOnlyList<ProviderMetadataResult>>(StringComparer.Ordinal);
        var misses = new List<IMetadataProvider>();
        foreach (IMetadataProvider provider in _providers)
        {
            ProviderCacheEntryRow? entry = cached.FirstOrDefault(
                row => string.Equals(row.Provider, provider.ProviderName, StringComparison.Ordinal));
            if (entry is null || entry.ExpiresUtc < now)
            {
                if (entry is not null && !entry.IsNegative)
                {
                    staleResults[provider.ProviderName] = Deserialize(entry.ResponseJson)
                        .Select(result => result with { IsStale = true })
                        .ToArray();
                }
                misses.Add(provider);
                continue;
            }

            if (!entry.IsNegative)
            {
                results.AddRange(Deserialize(entry.ResponseJson));
            }
        }

        ProviderMetadataResult?[] fetched = await Task.WhenAll(
            misses.Select(provider => FetchSafelyAsync(
                provider, NormalizeRequest(request), cancellationToken)))
            .ConfigureAwait(false);
        foreach (ProviderMetadataResult? result in fetched)
        {
            if (result is null)
            {
                continue;
            }

            results.Add(result);
            string providerName = result.Provider;
            ProviderCacheEntryRow? entry = cached.FirstOrDefault(
                row => string.Equals(row.Provider, providerName, StringComparison.Ordinal));
            string responseJson = JsonSerializer.Serialize(new[] { result });
            if (responseJson.Length > 262144)
            {
                responseJson = JsonSerializer.Serialize(Array.Empty<ProviderMetadataResult>());
            }

            if (entry is null)
            {
                context.ProviderCacheEntries.Add(new ProviderCacheEntryRow
                {
                    Provider = providerName,
                    QueryKey = queryKey,
                    ResponseJson = responseJson,
                    IsNegative = false,
                    RetrievedUtc = now,
                    ExpiresUtc = now.Add(SuccessTtl),
                    ContractVersion = ContractVersion,
                });
            }
            else
            {
                entry.ResponseJson = responseJson;
                entry.IsNegative = false;
                entry.RetrievedUtc = now;
                entry.ExpiresUtc = now.Add(SuccessTtl);
            }
        }

        foreach (IMetadataProvider provider in misses.Where(provider =>
                     !fetched.Any(result => result?.Provider == provider.ProviderName)))
        {
            if (staleResults.TryGetValue(provider.ProviderName, out IReadOnlyList<ProviderMetadataResult>? stale))
            {
                results.AddRange(stale);
                continue;
            }

            ProviderCacheEntryRow? entry = cached.FirstOrDefault(
                row => string.Equals(row.Provider, provider.ProviderName, StringComparison.Ordinal));
            if (entry is null)
            {
                context.ProviderCacheEntries.Add(new ProviderCacheEntryRow
                {
                    Provider = provider.ProviderName,
                    QueryKey = queryKey,
                    ResponseJson = "[]",
                    IsNegative = true,
                    RetrievedUtc = now,
                    ExpiresUtc = now.Add(NegativeTtl),
                    ContractVersion = ContractVersion,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    private static async Task<ProviderMetadataResult?> FetchSafelyAsync(
        IMetadataProvider provider,
        MetadataLookupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ProviderMetadataResult> results = await provider
                .SearchAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return results.Count > 0 ? results[0] : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static List<ProviderMetadataResult> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ProviderMetadataResult>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeQuery(MetadataLookupRequest request) =>
        string.Join(
            "|",
            $"isbn:{Normalize(request.Isbn13)}",
            $"title:{Normalize(request.Title)}",
            $"author:{Normalize(request.Author)}");

    private static MetadataLookupRequest NormalizeRequest(MetadataLookupRequest request) => new(
        NormalizeNullable(request.Isbn13),
        NormalizeNullable(request.Title),
        NormalizeNullable(request.Author));

    private static string? NormalizeNullable(string? value)
    {
        string normalized = Normalize(value);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private async Task<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;
        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
