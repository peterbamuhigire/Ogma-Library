using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Backend service for the Phase 10 Index Manager dashboard and rebuild flow.
/// </summary>
public sealed class IndexManagerService : IIndexManagerService, ISearchReadModel
{
    private const int ActiveBookStatus = 0;
    private const int RebuildBatchSize = 5;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IExtractionPipelineService _pipeline;
    private readonly IFtsIndexService _ftsIndex;
    private readonly ObservableEvents<IndexStatusUpdate> _events = new();
    private readonly ObservableEvents<SearchIndexEvent> _searchEvents = new();

    /// <summary>
    /// Initializes a new instance of <see cref="IndexManagerService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public IndexManagerService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IExtractionPipelineService pipeline,
        IFtsIndexService ftsIndex)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(ftsIndex);

        _contextFactory = contextFactory;
        _pipeline = pipeline;
        _ftsIndex = ftsIndex;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IndexManagerService"/> for tests
    /// that share one context.
    /// </summary>
    internal IndexManagerService(
        CatalogueDbContext context,
        IExtractionPipelineService pipeline,
        IFtsIndexService ftsIndex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(ftsIndex);

        _context = context;
        _pipeline = pipeline;
        _ftsIndex = ftsIndex;
    }

    /// <inheritdoc />
    public IObservable<IndexStatusUpdate> Events => _events;

    /// <inheritdoc />
    IObservable<SearchIndexEvent> ISearchReadModel.Events => _searchEvents;

    /// <inheritdoc />
    public async Task<IndexManagerStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var bookRows = await context.Books
            .AsNoTracking()
            .Where(book => book.Status == ActiveBookStatus)
            .Select(book => new
            {
                book.BookId,
                book.Title,
                book.IndexStatus,
                ExtractedPageCount = context.ExtractedPages.Count(page => page.BookId == book.BookId),
                SearchChunkCount = context.SearchChunks.Count(chunk => chunk.BookId == book.BookId),
                FailedPageCount = context.ExtractedPages.Count(page =>
                    page.BookId == book.BookId &&
                    page.ExtractionQuality == (int)SearchExtractionQuality.Failed),
                PendingOcrPageCount = context.ExtractedPages.Count(page =>
                    page.BookId == book.BookId &&
                    page.ExtractionQuality == (int)SearchExtractionQuality.Scanned),
            })
            .OrderBy(book => book.Title)
            .ThenBy(book => book.BookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        FtsIntegrityResult integrity = await _ftsIndex.CheckIntegrityAsync(cancellationToken)
            .ConfigureAwait(false);
        int chunkCount = await context.SearchChunks.CountAsync(cancellationToken).ConfigureAwait(false);
        long indexSizeBytes = await context.SearchChunks
            .Select(chunk => chunk.ChunkText == null ? 0 : chunk.ChunkText.Length)
            .SumAsync(cancellationToken)
            .ConfigureAwait(false);

        IndexManagerStatus status = new(
            TotalBooks: bookRows.Count,
            IndexedBooks: bookRows.Count(book => book.IndexStatus == (int)SearchBookIndexStatus.Indexed),
            ExtractingBooks: bookRows.Count(book => book.IndexStatus == (int)SearchBookIndexStatus.Extracting),
            FailedBooks: bookRows.Count(book => book.IndexStatus == (int)SearchBookIndexStatus.Failed),
            PendingOcrPages: bookRows.Sum(book => book.PendingOcrPageCount),
            FailedExtractionPages: bookRows.Sum(book => book.FailedPageCount),
            SearchChunkCount: chunkCount,
            IndexSizeBytes: indexSizeBytes,
            Integrity: integrity,
            Books: bookRows
                .Select(book => new BookIndexStatusItem(
                    book.BookId,
                    book.Title,
                    (SearchBookIndexStatus)book.IndexStatus,
                    book.ExtractedPageCount,
                    book.SearchChunkCount,
                    book.FailedPageCount,
                    book.PendingOcrPageCount))
                .ToList());

        _events.Publish(new IndexStatusUpdate.StatusChanged(status));
        return status;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildResult> RebuildAsync(CancellationToken cancellationToken)
    {
        long startedTimestamp = TimeProvider.System.GetTimestamp();
        _events.Publish(new IndexStatusUpdate.RebuildStarted(DateTimeOffset.UtcNow));
        await ResetIndexAsync(cancellationToken).ConfigureAwait(false);

        int attempted = 0;
        int indexed = 0;
        int failed = 0;
        int chunksWritten = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ExtractionBatchResult batch = await _pipeline
                    .IndexNextBatchAsync(RebuildBatchSize, cancellationToken)
                    .ConfigureAwait(false);
                if (batch.BooksAttempted == 0)
                {
                    break;
                }

                attempted += batch.BooksAttempted;
                indexed += batch.BooksIndexed;
                failed += batch.BooksFailed;
                chunksWritten += batch.ChunksWritten;
            }

            FtsIntegrityResult integrity = await _ftsIndex.CheckIntegrityAsync(cancellationToken)
                .ConfigureAwait(false);
            IndexRebuildResult result = new(
                Completed: !cancellationToken.IsCancellationRequested && integrity.IsHealthy,
                BooksAttempted: attempted,
                BooksIndexed: indexed,
                BooksFailed: failed,
                ChunksWritten: chunksWritten,
                IntegrityHealthy: integrity.IsHealthy,
                ErrorMessage: integrity.ErrorMessage);
            _events.Publish(new IndexStatusUpdate.RebuildCompleted(result));
            IndexManagerStatus? status = await PublishStatusAsync(cancellationToken).ConfigureAwait(false);
            if (result.Completed && status is not null)
            {
                PublishSearchReadModelEvents(status, startedTimestamp);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            IndexRebuildResult result = new(
                Completed: false,
                BooksAttempted: attempted,
                BooksIndexed: indexed,
                BooksFailed: failed,
                ChunksWritten: chunksWritten,
                IntegrityHealthy: false,
                ErrorMessage: "Rebuild cancelled.");
            _events.Publish(new IndexStatusUpdate.RebuildCompleted(result));
            throw;
        }
    }

    private async Task ResetIndexAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await context.SearchChunks.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.ExtractedPages.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Books
            .Where(book => book.Status == ActiveBookStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(book => book.IndexStatus, (int)SearchBookIndexStatus.NotIndexed),
                cancellationToken)
            .ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        context.ChangeTracker.Clear();
    }

    private async Task<IndexManagerStatus?> PublishStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Status publication must not mask a completed rebuild result.
            return null;
        }
    }

    private void PublishSearchReadModelEvents(IndexManagerStatus status, long startedTimestamp)
    {
        DateTimeOffset publishedAtUtc = DateTimeOffset.UtcNow;
        foreach (BookIndexStatusItem book in status.Books)
        {
            switch (book.Status)
            {
                case SearchBookIndexStatus.Indexed:
                    _searchEvents.Publish(new SearchIndexEvent.BookIndexed(
                        book.BookId,
                        book.SearchChunkCount,
                        publishedAtUtc));
                    break;
                case SearchBookIndexStatus.Failed:
                    _searchEvents.Publish(new SearchIndexEvent.BookIndexFailed(
                        book.BookId,
                        "Indexing failed.",
                        publishedAtUtc));
                    break;
            }
        }

        _searchEvents.Publish(new SearchIndexEvent.IndexRebuilt(
            status.SearchChunkCount,
            (long)TimeProvider.System.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            publishedAtUtc));
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private sealed class ObservableEvents<TEvent> : IObservable<TEvent>
    {
        private readonly object _gate = new();
        private readonly List<IObserver<TEvent>> _observers = [];

        public IDisposable Subscribe(IObserver<TEvent> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_gate)
            {
                _observers.Add(observer);
            }

            return new Subscription(this, observer);
        }

        public void Publish(TEvent update)
        {
            IObserver<TEvent>[] observers;
            lock (_gate)
            {
                observers = _observers.ToArray();
            }

            foreach (IObserver<TEvent> observer in observers)
            {
                observer.OnNext(update);
            }
        }

        private void Unsubscribe(IObserver<TEvent> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly ObservableEvents<TEvent> _owner;
            private IObserver<TEvent>? _observer;

            public Subscription(ObservableEvents<TEvent> owner, IObserver<TEvent> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                IObserver<TEvent>? observer = Interlocked.Exchange(ref _observer, null);
                if (observer is not null)
                {
                    _owner.Unsubscribe(observer);
                }
            }
        }
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
