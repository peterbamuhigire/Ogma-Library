using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// The search destination's pipeline (Sept-23 Phase 13, ADR-0019): parser → structured
/// filters → metadata (exact, prefix, typo-tolerant fallback) → FTS5 (all words, any
/// order) → semantic results only when a provider answers → reciprocal rank fusion into
/// one result per catalogue book, with display titles resolved like the catalogue.
/// </summary>
public sealed class UnifiedSearchService : IUnifiedSearchService
{
    private const int FullTextWindow = 100;
    private const double MetadataWeight = 1.0;
    private const double FullTextWeight = 1.0;
    private const double SemanticWeight = 0.9;
    private static readonly TimeSpan ProbeTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ProbeBudget = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SnapshotMaxAge = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IFtsIndexService _fullText;
    private readonly ISemanticSearchService? _semantic;
    private readonly IOllamaEmbeddingProvider? _provider;
    private readonly ILogger _logger;
    private readonly object _probeGate = new();
    private Task<bool>? _probe;
    private bool _probeValue;
    private DateTimeOffset _probeExpiresUtc = DateTimeOffset.MinValue;
    private CatalogueSnapshot? _snapshot;

    /// <summary>Initializes the unified search pipeline.</summary>
    [ActivatorUtilitiesConstructor]
    public UnifiedSearchService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IFtsIndexService fullText,
        ISemanticSearchService semantic,
        IOllamaEmbeddingProvider provider,
        ILogger<UnifiedSearchService>? logger = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _fullText = fullText ?? throw new ArgumentNullException(nameof(fullText));
        _semantic = semantic ?? throw new ArgumentNullException(nameof(semantic));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <summary>Initializes the pipeline over one shared context (tests).</summary>
    internal UnifiedSearchService(
        CatalogueDbContext context,
        IFtsIndexService fullText,
        ISemanticSearchService? semantic = null,
        IOllamaEmbeddingProvider? provider = null,
        ILogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _fullText = fullText ?? throw new ArgumentNullException(nameof(fullText));
        _semantic = semantic;
        _provider = provider;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<UnifiedSearchResponse> SearchAsync(
        string? queryText,
        int maxResults,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);
        var clock = Stopwatch.StartNew();
        ParsedSearchQuery query = SearchQueryParser.Parse(queryText);

        CatalogueSnapshot snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SearchBookCandidate> books = snapshot.Books;
        SearchIndexCoverage coverage = snapshot.Coverage;
        if (query.IsEmpty)
        {
            return new UnifiedSearchResponse(query, [], SemanticSearchState.NotApplicable, false, coverage, clock.Elapsed);
        }

        Task<(SemanticSearchState State, IReadOnlyList<SemanticSearchResult> Results)> semanticTask =
            RunSemanticAsync(query, maxResults, cancellationToken);
        MetadataOutcome metadata = MetadataMatcher.Match(query, snapshot.Prepared);
        Dictionary<string, SearchBookCandidate> visible = books.ToDictionary(book => book.BookId, StringComparer.Ordinal);

        bool Admit(string bookId) =>
            visible.ContainsKey(bookId) &&
            !metadata.Excluded.Contains(bookId) &&
            (metadata.Allowed is null || metadata.Allowed.Contains(bookId));

        IReadOnlyList<FtsSearchResult> fullText = await _fullText
            .SearchAsync(query.Text, FullTextWindow, cancellationToken)
            .ConfigureAwait(false);
        List<IGrouping<string, FtsSearchResult>> textByBook = fullText
            .Where(hit => Admit(hit.BookId))
            .GroupBy(hit => hit.BookId, StringComparer.Ordinal)
            .ToList();

        (SemanticSearchState semanticState, IReadOnlyList<SemanticSearchResult> semanticResults) =
            await semanticTask.ConfigureAwait(false);
        List<SemanticSearchResult> semantic = semanticResults.Where(result => Admit(result.BookId)).ToList();

        List<MetadataHit> metadataHits = metadata.Hits.Where(hit => Admit(hit.BookId)).ToList();
        bool usedTypoTolerance = false;
        if (metadataHits.Count == 0 && textByBook.Count == 0 && semantic.Count == 0)
        {
            metadataHits = metadata.FuzzyHits.Where(hit => Admit(hit.BookId)).ToList();
            usedTypoTolerance = metadataHits.Count > 0;
        }

        List<UnifiedSearchResult> results = Fuse(visible, metadataHits, textByBook, semantic, maxResults);
        clock.Stop();
        SearchLog.Completed(
            _logger,
            results.Count,
            metadataHits.Count,
            textByBook.Count,
            semantic.Count,
            clock.ElapsedMilliseconds,
            semanticState);
        return new UnifiedSearchResponse(query, results, semanticState, usedTypoTolerance, coverage, clock.Elapsed);
    }

    /// <inheritdoc />
    public async Task<SearchIndexCoverage> GetCoverageAsync(CancellationToken cancellationToken)
    {
        CatalogueSnapshot snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Coverage;
    }

    private static List<UnifiedSearchResult> Fuse(
        Dictionary<string, SearchBookCandidate> visible,
        List<MetadataHit> metadataHits,
        List<IGrouping<string, FtsSearchResult>> textByBook,
        List<SemanticSearchResult> semantic,
        int maxResults)
    {
        var fused = new Dictionary<string, FusedBook>(StringComparer.Ordinal);
        FusedBook Get(string bookId)
        {
            if (!fused.TryGetValue(bookId, out FusedBook? book))
            {
                book = new FusedBook(visible[bookId]);
                fused.Add(bookId, book);
            }

            return book;
        }

        for (int rank = 0; rank < metadataHits.Count; rank++)
        {
            MetadataHit hit = metadataHits[rank];
            FusedBook book = Get(hit.BookId);
            book.Score += MetadataWeight / (UnifiedSearchContract.RrfConstant + rank + 1);
            book.MetadataScore = hit.Score;
            book.Metadata = hit;
        }

        // Full-text hits arrive ordered by bm25; the first group is the best book.
        for (int rank = 0; rank < textByBook.Count; rank++)
        {
            IGrouping<string, FtsSearchResult> group = textByBook[rank];
            FusedBook book = Get(group.Key);
            book.Score += FullTextWeight / (UnifiedSearchContract.RrfConstant + rank + 1);
            book.TextHits = group.ToList();
            book.TextScore = book.TextHits.Max(hit => hit.Score);
        }

        List<SemanticSearchResult> semanticOrdered = semantic
            .OrderByDescending(result => result.SemanticScore ?? 0)
            .ThenBy(result => result.BookId, StringComparer.Ordinal)
            .ToList();
        for (int rank = 0; rank < semanticOrdered.Count; rank++)
        {
            FusedBook book = Get(semanticOrdered[rank].BookId);
            book.Score += SemanticWeight / (UnifiedSearchContract.RrfConstant + rank + 1);
            book.Semantic ??= semanticOrdered[rank];
        }

        return fused.Values
            .OrderByDescending(book => book.Score)
            .ThenByDescending(book => book.MetadataScore)
            .ThenByDescending(book => book.TextScore)
            .ThenBy(book => book.Candidate.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(book => book.Candidate.BookId, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(book => book.ToResult())
            .ToList();
    }

    private async Task<(SemanticSearchState State, IReadOnlyList<SemanticSearchResult> Results)> RunSemanticAsync(
        ParsedSearchQuery query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (!query.HasFreeText)
        {
            return (SemanticSearchState.NotApplicable, []);
        }

        if (_semantic is null || _provider is null ||
            !await IsProviderAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return (SemanticSearchState.Unavailable, []);
        }

        try
        {
            string text = string.Join(' ', query.Terms.Concat(query.Phrases));
            SemanticSearchResponse response = await _semantic
                .SearchAsync(text, maxResults, cancellationToken)
                .ConfigureAwait(false);
            if (response.ProviderUnavailable)
            {
                return (SemanticSearchState.Unavailable, []);
            }

            if (response.UsedExactFallback)
            {
                return (response.Availability == SemanticSearchAvailability.NoIndex
                    ? SemanticSearchState.Preparing
                    : SemanticSearchState.Unavailable, []);
            }

            return (SemanticSearchState.Active,
                response.Results.Where(result => result.SemanticScore is not null).ToList());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The optional stage must never take keyword search down with it.
            SearchLog.SemanticFailed(_logger, exception);
            return (SemanticSearchState.Unavailable, []);
        }
    }

    /// <summary>
    /// Cached provider probe: at most one probe a minute, and a search waits for a running
    /// probe for at most <see cref="ProbeBudget"/> before using the last known answer.
    /// </summary>
    private async Task<bool> IsProviderAvailableAsync(CancellationToken cancellationToken)
    {
        Task<bool> probe;
        lock (_probeGate)
        {
            if (DateTimeOffset.UtcNow < _probeExpiresUtc)
            {
                return _probeValue;
            }

            _probe ??= ProbeAsync();
            probe = _probe;
        }

        Task finished = await Task.WhenAny(probe, Task.Delay(ProbeBudget, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (finished == probe)
        {
            return await probe.ConfigureAwait(false);
        }

        lock (_probeGate)
        {
            return _probeValue;
        }
    }

    private async Task<bool> ProbeAsync()
    {
        bool available;
        using var timeout = new CancellationTokenSource(ProbeTimeout);
        try
        {
            available = await _provider!.IsAvailableAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            available = false;
        }
        catch (Exception exception)
        {
            SearchLog.ProbeFailed(_logger, exception);
            available = false;
        }

        lock (_probeGate)
        {
            _probeValue = available;
            _probeExpiresUtc = DateTimeOffset.UtcNow + ProbeTtl;
            _probe = null;
        }

        return available;
    }

    /// <summary>
    /// Returns the cached catalogue snapshot, reloading it when a cheap change signature of
    /// the catalogue tables differs (or after <see cref="SnapshotMaxAge"/>). Typing a query
    /// therefore loads and folds the catalogue once, not on every keystroke.
    /// </summary>
    private async Task<CatalogueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (context.ConfigureAwait(false))
            {
                return await GetSnapshotAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }

        // Tests share one context; the pipeline uses it sequentially.
        return await GetSnapshotAsync(_context!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CatalogueSnapshot> GetSnapshotAsync(CatalogueDbContext context, CancellationToken cancellationToken)
    {
        string signature = await ReadSignatureAsync(context, cancellationToken).ConfigureAwait(false);
        CatalogueSnapshot? cached = Volatile.Read(ref _snapshot);
        if (cached is not null &&
            string.Equals(cached.Signature, signature, StringComparison.Ordinal) &&
            DateTimeOffset.UtcNow - cached.LoadedUtc < SnapshotMaxAge)
        {
            return cached;
        }

        List<SearchBookCandidate> books = await SearchBookCandidateLoader
            .LoadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        var snapshot = new CatalogueSnapshot(
            signature,
            DateTimeOffset.UtcNow,
            books,
            MetadataMatcher.Prepare(books),
            SearchBookCandidateLoader.Coverage(books));
        Volatile.Write(ref _snapshot, snapshot);
        return snapshot;
    }

    private static async Task<string> ReadSignatureAsync(CatalogueDbContext context, CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) || '.' || IFNULL(SUM(Status + 3 * TextStatus + 17 * IndexStatus + 31 * IsPasswordProtected + LENGTH(IFNULL(Title, '')) + LENGTH(IFNULL(IsbnNormalized, '')) + IFNULL(Year, 0)), 0) FROM Books)
                || '|' || (SELECT COUNT(*) || '.' || IFNULL(MAX(FieldId), 0) || '.' || IFNULL(SUM(LENGTH(IFNULL(Value, '')) + IsOverridden), 0) FROM BookMetadataFields)
                || '|' || (SELECT COUNT(*) || '.' || IFNULL(SUM(AuthorId + DisplayOrder), 0) FROM BookAuthors)
                || '|' || (SELECT COUNT(*) || '.' || IFNULL(SUM(FileStatus + 7 * FileValidity), 0) FROM BookFiles)
                || '|' || (SELECT COUNT(*) FROM ShelfBooks)
                || '|' || (SELECT COUNT(*) || '.' || IFNULL(SUM(LENGTH(Name)), 0) FROM Shelves)
                || '|' || (SELECT COUNT(*) || '.' || IFNULL(SUM(IsEnabled), 0) || '.' || COUNT(RemovedUtc) FROM LibraryRoots);
            """;
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed record CatalogueSnapshot(
        string Signature,
        DateTimeOffset LoadedUtc,
        IReadOnlyList<SearchBookCandidate> Books,
        IReadOnlyList<MetadataMatcher.FoldedSearchBook> Prepared,
        SearchIndexCoverage Coverage);

    private sealed class FusedBook(SearchBookCandidate candidate)
    {
        public SearchBookCandidate Candidate { get; } = candidate;

        public double Score { get; set; }

        public double MetadataScore { get; set; }

        public double TextScore { get; set; } = double.MinValue;

        public MetadataHit? Metadata { get; set; }

        public List<FtsSearchResult> TextHits { get; set; } = [];

        public SemanticSearchResult? Semantic { get; set; }

        public UnifiedSearchResult ToResult()
        {
            var matches = new List<UnifiedMatch>();
            if (Metadata is not null)
            {
                matches.AddRange(Metadata.Matches);
            }

            foreach (FtsSearchResult hit in TextHits)
            {
                matches.Add(new UnifiedMatch(KindOf(hit.Source), hit.PageIndex, IsOcrText: hit.IsOcrText));
            }

            if (Semantic is not null)
            {
                matches.Add(new UnifiedMatch(UnifiedMatchKind.Semantic, Semantic.PageIndex, IsOcrText: Semantic.IsOcrText));
            }

            FtsSearchResult? best = TextHits.FirstOrDefault();
            FtsSearchResult? bestPage = TextHits.FirstOrDefault(hit => hit.PageJumpTarget is not null);
            SearchSnippet? snippet = best?.HighlightedSnippet ??
                (Semantic?.Snippet is { Length: > 0 } semanticSnippet ? new SearchSnippet(semanticSnippet, []) : null);
            return new UnifiedSearchResult(
                Candidate.BookId,
                Candidate.DisplayTitle,
                Candidate.DisplayAuthor,
                Score,
                matches,
                snippet,
                bestPage?.PageJumpTarget ?? Semantic?.PageJumpTarget,
                Metadata?.CorrectionSuggestion);
        }

        private static UnifiedMatchKind KindOf(SearchChunkSource source) => source switch
        {
            SearchChunkSource.Note => UnifiedMatchKind.Note,
            SearchChunkSource.Tag => UnifiedMatchKind.Tag,
            SearchChunkSource.Description => UnifiedMatchKind.Description,
            SearchChunkSource.Toc => UnifiedMatchKind.Toc,
            _ => UnifiedMatchKind.Page,
        };
    }
}
