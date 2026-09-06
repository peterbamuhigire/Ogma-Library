using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// Backend service for the Phase 10 Index Manager dashboard and rebuild flow.
/// </summary>
public sealed class IndexManagerService : IIndexManagerService, ISearchReadModel, IDisposable
{
    private const int ActiveBookStatus = 0;
    private const int RebuildBatchSize = 5;
    private const string OcrJobType = "OcrJob";
    private const int RebuildRunning = 1;
    private const int RebuildCompleted = 2;
    private const string ActiveIndexVersion = "fts5-v1";
    private const string StagingIndexPrefix = "fts5-rebuild-";

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IExtractionPipelineService _pipeline;
    private readonly IFtsIndexService _ftsIndex;
    private readonly IEmbeddingVectorRepository _vectors;
    private readonly SemaphoreSlim _rebuildGate = new(1, 1);
    private readonly ObservableEvents<IndexStatusUpdate> _events = new();
    private readonly ObservableEvents<SearchIndexEvent> _searchEvents = new();

    /// <summary>
    /// Initializes a new instance of <see cref="IndexManagerService"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public IndexManagerService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IExtractionPipelineService pipeline,
        IFtsIndexService ftsIndex,
        IEmbeddingVectorRepository vectors)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(ftsIndex);
        ArgumentNullException.ThrowIfNull(vectors);

        _contextFactory = contextFactory;
        _pipeline = pipeline;
        _ftsIndex = ftsIndex;
        _vectors = vectors;
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
        _vectors = new EmbeddingVectorRepository(context);
    }

    /// <inheritdoc />
    public IObservable<IndexStatusUpdate> Events => _events;

    /// <inheritdoc />
    IObservable<SearchIndexEvent> ISearchReadModel.Events => _searchEvents;

    /// <summary>Releases the in-process rebuild gate.</summary>
    public void Dispose() => _rebuildGate.Dispose();

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
        List<OcrJobStatusItem> ocrJobs = await LoadOcrJobStatusesAsync(context, cancellationToken)
            .ConfigureAwait(false);
        SmartShelfQueryStats smartShelfStats = await LoadSmartShelfStatsAsync(context, cancellationToken)
            .ConfigureAwait(false);
        int staleEmbeddingCount = await _vectors
            .GetStaleCountAsync(null, cancellationToken)
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
                .ToList(),
            OcrJobs: ocrJobs,
            SmartShelfStats: smartShelfStats,
            StaleEmbeddingCount: staleEmbeddingCount);

        _events.Publish(new IndexStatusUpdate.StatusChanged(status));
        return status;
    }

    /// <inheritdoc />
    public async Task<IndexRebuildResult> RebuildAsync(CancellationToken cancellationToken)
    {
        await _rebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long startedTimestamp = TimeProvider.System.GetTimestamp();
            _events.Publish(new IndexStatusUpdate.RebuildStarted(DateTimeOffset.UtcNow));
            IStagedExtractionPipelineService? stagedPipeline = _pipeline as IStagedExtractionPipelineService;
            SearchRebuildCheckpointRow checkpoint = await LoadOrStartCheckpointAsync(
                    stagedPipeline is not null,
                    cancellationToken)
                .ConfigureAwait(false);
            string stagingIndexVersion = StagingIndexPrefix + checkpoint.RebuildId;

            int attempted = checkpoint.BooksAttempted;
            int indexed = checkpoint.BooksIndexed;
            int failed = checkpoint.BooksFailed;
            int chunksWritten = checkpoint.ChunksWritten;
            while (!cancellationToken.IsCancellationRequested)
            {
                ExtractionBatchResult batch = stagedPipeline is null
                    ? await _pipeline.IndexNextBatchAsync(RebuildBatchSize, cancellationToken).ConfigureAwait(false)
                    : await stagedPipeline
                        .IndexNextBatchAsync(RebuildBatchSize, stagingIndexVersion, cancellationToken)
                        .ConfigureAwait(false);
                if (batch.BooksAttempted == 0)
                {
                    break;
                }

                attempted += batch.BooksAttempted;
                indexed += batch.BooksIndexed;
                failed += batch.BooksFailed;
                chunksWritten += batch.ChunksWritten;
                await SaveCheckpointAsync(
                        checkpoint.SearchRebuildCheckpointId,
                        RebuildRunning,
                        attempted,
                        indexed,
                        failed,
                        chunksWritten,
                        errorMessage: null,
                        completedUtc: null,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            FtsIntegrityResult integrity = await _ftsIndex.CheckIntegrityAsync(cancellationToken)
                .ConfigureAwait(false);
            bool canPromote = !cancellationToken.IsCancellationRequested &&
                              integrity.IsHealthy &&
                              (stagedPipeline is null || failed == 0);
            if (canPromote && stagedPipeline is not null)
            {
                await PromoteStagedIndexAsync(stagingIndexVersion, cancellationToken).ConfigureAwait(false);
            }

            IndexRebuildResult result = new(
                Completed: canPromote,
                BooksAttempted: attempted,
                BooksIndexed: indexed,
                BooksFailed: failed,
                ChunksWritten: chunksWritten,
                IntegrityHealthy: integrity.IsHealthy,
                ErrorMessage: integrity.ErrorMessage ??
                    (failed > 0 ? "Staged rebuild retained the active index because one or more books failed." : null));
            await SaveCheckpointAsync(
                    checkpoint.SearchRebuildCheckpointId,
                    RebuildCompleted,
                    attempted,
                    indexed,
                    failed,
                    chunksWritten,
                    result.ErrorMessage,
                    DateTimeOffset.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);
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
            // A running checkpoint is intentionally retained. The next rebuild
            // continues from durable book/page state instead of clearing it.
            throw;
        }
        finally
        {
            _rebuildGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task PauseOcrJobAsync(long jobId, CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        int updated = await context.Jobs
            .Where(job =>
                job.JobId == jobId &&
                job.JobType == OcrJobType &&
                (job.Status == (int)JobRuntimeStatus.Pending ||
                 job.Status == (int)JobRuntimeStatus.Running))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, (int)JobRuntimeStatus.Paused)
                    .SetProperty(job => job.StartedUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.CompletedUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.LeaseOwner, (string?)null)
                    .SetProperty(job => job.LeaseExpiresUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.NextAttemptUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.FailureCode, "paused_by_user")
                    .SetProperty(job => job.ErrorMessage, "Paused by user."),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated > 0)
        {
            AddOcrControlAuditEvent(context, "OcrJobPaused", jobId, "paused");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (updated > 0)
        {
            _ = await PublishStatusAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task CancelOcrJobAsync(long jobId, CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
        int updated = await context.Jobs
            .Where(job =>
                job.JobId == jobId &&
                job.JobType == OcrJobType &&
                (job.Status == (int)JobRuntimeStatus.Pending ||
                 job.Status == (int)JobRuntimeStatus.Running ||
                 job.Status == (int)JobRuntimeStatus.Failed ||
                 job.Status == (int)JobRuntimeStatus.Paused))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, (int)JobRuntimeStatus.Cancelled)
                    .SetProperty(job => job.StartedUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.CompletedUtc, completedUtc)
                    .SetProperty(job => job.LeaseOwner, (string?)null)
                    .SetProperty(job => job.LeaseExpiresUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.NextAttemptUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.FailureCode, "cancelled_by_user")
                    .SetProperty(job => job.ErrorMessage, "Cancelled by user."),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated > 0)
        {
            AddOcrControlAuditEvent(context, "OcrJobCancelled", jobId, "cancelled");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (updated > 0)
        {
            _ = await PublishStatusAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RetryOcrJobAsync(long jobId, CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        int updated = await context.Jobs
            .Where(job =>
                job.JobId == jobId &&
                job.JobType == OcrJobType &&
                (job.Status == (int)JobRuntimeStatus.Failed ||
                 job.Status == (int)JobRuntimeStatus.Cancelled ||
                 job.Status == (int)JobRuntimeStatus.Paused))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, (int)JobRuntimeStatus.Pending)
                    .SetProperty(job => job.StartedUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.CompletedUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.LeaseOwner, (string?)null)
                    .SetProperty(job => job.LeaseExpiresUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.NextAttemptUtc, (DateTimeOffset?)null)
                    .SetProperty(job => job.FailureCode, (string?)null)
                    .SetProperty(job => job.ErrorMessage, (string?)null)
                    .SetProperty(job => job.RetryCount, job => job.RetryCount + 1),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated > 0)
        {
            AddOcrControlAuditEvent(context, "OcrJobRetried", jobId, "pending");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (updated > 0)
        {
            _ = await PublishStatusAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task<SearchRebuildCheckpointRow> LoadOrStartCheckpointAsync(
        bool sideBySide,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<SearchRebuildCheckpointRow> running = await lease.Context.SearchRebuildCheckpoints
            .Where(row => row.Status == RebuildRunning)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        SearchRebuildCheckpointRow? existing = running
            .OrderByDescending(row => row.UpdatedUtc)
            .FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        _ = await _ftsIndex.CleanupStaleAsync(cancellationToken).ConfigureAwait(false);
        if (sideBySide)
        {
            await PrepareStagedRebuildAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ResetIndexAsync(cancellationToken).ConfigureAwait(false);
        }

        var checkpoint = new SearchRebuildCheckpointRow
        {
            RebuildId = Guid.NewGuid().ToString("N"),
            Status = RebuildRunning,
            StartedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };
        lease.Context.SearchRebuildCheckpoints.Add(checkpoint);
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return checkpoint;
    }

    private async Task PrepareStagedRebuildAsync(CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        await lease.Context.Books
            .Where(book => book.Status == ActiveBookStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    book => book.IndexStatus,
                    (int)SearchBookIndexStatus.NotIndexed),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PromoteStagedIndexAsync(
        string stagingIndexVersion,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await lease.Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await lease.Context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM SearchChunks WHERE IndexVersion = {ActiveIndexVersion};",
                cancellationToken)
            .ConfigureAwait(false);
        await lease.Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE SearchChunks SET IndexVersion = {ActiveIndexVersion} WHERE IndexVersion = {stagingIndexVersion};",
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveCheckpointAsync(
        long checkpointId,
        int status,
        int attempted,
        int indexed,
        int failed,
        int chunksWritten,
        string? errorMessage,
        DateTimeOffset? completedUtc,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        SearchRebuildCheckpointRow? checkpoint = await lease.Context.SearchRebuildCheckpoints
            .FirstOrDefaultAsync(row => row.SearchRebuildCheckpointId == checkpointId, cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is null)
        {
            return;
        }

        checkpoint.Status = status;
        checkpoint.BooksAttempted = attempted;
        checkpoint.BooksIndexed = indexed;
        checkpoint.BooksFailed = failed;
        checkpoint.ChunksWritten = chunksWritten;
        checkpoint.ErrorMessage = errorMessage;
        checkpoint.CompletedUtc = completedUtc;
        checkpoint.UpdatedUtc = DateTimeOffset.UtcNow;
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task<List<OcrJobStatusItem>> LoadOcrJobStatusesAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        var jobs = await context.Jobs
            .AsNoTracking()
            .Where(job => job.JobType == OcrJobType)
            .GroupJoin(
                context.Books.AsNoTracking(),
                job => job.BookId,
                book => book.BookId,
                (job, books) => new { Job = job, Book = books.FirstOrDefault() })
            .OrderByDescending(row => row.Job.JobId)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return jobs
            .Select(row =>
            {
                (int processedPages, int totalPages) = ReadOcrProgress(row.Job.Payload);
                return new OcrJobStatusItem(
                    row.Job.JobId,
                    row.Job.BookId,
                    row.Book?.Title,
                    ToOcrState(row.Job.Status),
                    processedPages,
                    totalPages,
                    row.Job.ErrorMessage);
            })
            .ToList();
    }

    private static async Task<SmartShelfQueryStats> LoadSmartShelfStatsAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        string[] requiredIndexes =
        [
            "IX_Books_Status_Year",
            "IX_ShelfBooks_ShelfId_BookId",
            "IX_BookMetadataFields_FieldName_Value",
        ];

        List<string> missingIndexes = [];
        foreach (string index in requiredIndexes)
        {
            if (!IndexExists(context, index))
            {
                missingIndexes.Add(index);
            }
        }

        Stopwatch sw = Stopwatch.StartNew();
        _ = await context.Books
            .AsNoTracking()
            .Where(book => book.Status == ActiveBookStatus && book.Year >= 2010)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        sw.Stop();

        return new SmartShelfQueryStats(
            sw.Elapsed.TotalMilliseconds,
            missingIndexes.Count == 0,
            missingIndexes);
    }

    private static bool IndexExists(CatalogueDbContext context, string indexName)
    {
        context.Database.OpenConnection();
        using DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'index' AND name = $name
            """;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = indexName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static OcrJobState ToOcrState(int status) =>
        status switch
        {
            (int)JobRuntimeStatus.Pending => OcrJobState.Pending,
            (int)JobRuntimeStatus.Running => OcrJobState.Running,
            (int)JobRuntimeStatus.Completed => OcrJobState.Completed,
            (int)JobRuntimeStatus.Failed => OcrJobState.Failed,
            (int)JobRuntimeStatus.Cancelled => OcrJobState.Cancelled,
            (int)JobRuntimeStatus.Paused => OcrJobState.Paused,
            _ => OcrJobState.Failed,
        };

    private static void AddOcrControlAuditEvent(
        CatalogueDbContext context,
        string eventType,
        long jobId,
        string state)
    {
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = eventType,
            EntityId = jobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EntityType = "Job",
            AfterJson = JsonSerializer.Serialize(new { jobType = OcrJobType, state }),
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
    }

    private static (int ProcessedPages, int TotalPages) ReadOcrProgress(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (0, 0);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            return (
                ReadInt(root, "ProcessedPages", "processedPages"),
                ReadInt(root, "TotalPages", "totalPages"));
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }

    private static int ReadInt(JsonElement root, string pascalName, string camelName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (root.TryGetProperty(pascalName, out JsonElement pascalValue) &&
            pascalValue.TryGetInt32(out int pascalInt))
        {
            return Math.Max(0, pascalInt);
        }

        if (root.TryGetProperty(camelName, out JsonElement camelValue) &&
            camelValue.TryGetInt32(out int camelInt))
        {
            return Math.Max(0, camelInt);
        }

        return 0;
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
