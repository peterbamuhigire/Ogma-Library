using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Mode-aware catalogue read model for Standalone and Client modes.</summary>
public sealed class ClassroomCatalogueReadModel : ICatalogueReadModel
{
    private const int DefaultPageSize = 50;

    private readonly ICatalogueReadModel _local;
    private readonly IClassroomModeService _modeService;
    private readonly IClassroomHostConnectionService _connectionService;
    private readonly ILibraryHostClient _hostClient;

    /// <summary>Initializes a new instance of <see cref="ClassroomCatalogueReadModel"/>.</summary>
    /// <param name="local">The local standalone catalogue read model.</param>
    /// <param name="modeService">The runtime mode service.</param>
    /// <param name="connectionService">The active Host connection store.</param>
    /// <param name="hostClient">The typed Host API client.</param>
    public ClassroomCatalogueReadModel(
        ICatalogueReadModel local,
        IClassroomModeService modeService,
        IClassroomHostConnectionService connectionService,
        ILibraryHostClient hostClient)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
        _modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _hostClient = hostClient ?? throw new ArgumentNullException(nameof(hostClient));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
        CatalogueFilter filter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ClassroomHostConnection? connection = await GetClientConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            await foreach (BookSummaryProjection summary in _local
                .GetBookSummariesAsync(filter, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return summary;
            }

            yield break;
        }

        int pageSize = filter.MaxResults > 0
            ? Math.Clamp(filter.MaxResults, 1, DefaultPageSize)
            : DefaultPageSize;
        int returned = 0;
        int page = 1;
        bool hasMore;

        do
        {
            LibraryHostCataloguePage hostPage = await _hostClient
                .GetCataloguePageAsync(
                    connection.Request,
                    connection.SessionToken,
                    new LibraryHostCatalogueQuery(
                        Title: filter.TitleContains,
                        Author: filter.AuthorContains,
                        ShelfId: filter.ShelfId,
                        Status: filter.Status,
                        Page: page,
                        PageSize: pageSize),
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (LibraryHostBookSummary item in hostPage.Items)
            {
                if (filter.MaxResults > 0 && returned >= filter.MaxResults)
                {
                    yield break;
                }

                yield return MapSummary(item);
                returned++;
            }

            hasMore = hostPage.HasMore;
            page++;
        }
        while (hasMore);
    }

    /// <inheritdoc />
    public async Task<BookDetailProjection?> GetBookDetailAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ClassroomHostConnection? connection = await GetClientConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return await _local.GetBookDetailAsync(bookId, cancellationToken).ConfigureAwait(false);
        }

        LibraryHostBookDetail detail = await _hostClient
            .GetBookAsync(connection.Request, connection.SessionToken, bookId, cancellationToken)
            .ConfigureAwait(false);
        return MapDetail(detail);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ClassroomHostConnection? connection = await GetClientConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is not null)
        {
            yield break;
        }

        await foreach (ShelfProjection shelf in _local.GetShelvesAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return shelf;
        }
    }

    /// <inheritdoc />
    public async Task<ReadingProgressProjection?> GetProgressAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ClassroomHostConnection? connection = await GetClientConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return await _local.GetProgressAsync(bookId, cancellationToken).ConfigureAwait(false);
        }

        LibraryHostBookDetail detail = await _hostClient
            .GetBookAsync(connection.Request, connection.SessionToken, bookId, cancellationToken)
            .ConfigureAwait(false);
        return detail.ReadingProgress is null
            ? null
            : MapProgress(detail.ReadingProgress);
    }

    private async Task<ClassroomHostConnection?> GetClientConnectionAsync(CancellationToken cancellationToken)
    {
        ClassroomModeSettings mode = await _modeService.GetModeAsync(cancellationToken).ConfigureAwait(false);
        if (mode.Mode != LibraryRuntimeMode.ConnectToHost)
        {
            return null;
        }

        return await _connectionService.GetActiveAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BookSummaryProjection MapSummary(LibraryHostBookSummary item) =>
        new(
            BookId: item.BookId,
            Title: item.Title,
            Authors: item.Authors,
            CoverRelativePath: item.Assets.CoverUrl,
            Status: item.Status,
            Rating: item.Rating,
            ShelfIds: item.ShelfIds,
            ReadingProgressPct: item.ReadingProgressPct,
            IsAvailable: item.IsAvailable,
            Year: item.Year,
            Sha256Hash: item.ContentHash);

    private static BookDetailProjection MapDetail(LibraryHostBookDetail detail) =>
        new(
            BookId: detail.BookId,
            Title: detail.Title,
            Authors: detail.Authors,
            Year: detail.Year,
            Isbn: detail.Isbn,
            Doi: detail.Doi,
            Rating: detail.Rating,
            Status: detail.Status,
            CoverRelativePath: detail.Assets.CoverUrl,
            RelativePath: $"host://{detail.BookId}",
            Sha256Hash: null,
            SizeBytes: detail.SizeBytes,
            ReadingProgress: detail.ReadingProgress is null ? null : MapProgress(detail.ReadingProgress),
            Annotations: detail.Annotations,
            MetadataFields: detail.MetadataFields.Select(MapMetadataField).ToArray(),
            ReadingMemory: detail.ReadingMemory is null ? null : MapReadingMemory(detail.ReadingMemory),
            IsOcrDerived: detail.IsOcrDerived,
            IsPasswordProtected: detail.IsPasswordProtected);

    private static ReadingProgressProjection MapProgress(LibraryHostReadingProgress progress) =>
        new(
            progress.BookId,
            progress.CurrentPage,
            progress.CompletionPct,
            progress.LastReadUtc,
            progress.Status);

    private static MetadataFieldProjection MapMetadataField(LibraryHostMetadataField field) =>
        new(field.FieldName, field.Value, field.Source, field.Confidence, field.IsOverridden);

    private static ReadingMemorySummaryProjection MapReadingMemory(LibraryHostReadingMemorySummary memory) =>
        new(memory.Disposition, memory.KeyInsight, memory.UpdatedAtUtc);
}
