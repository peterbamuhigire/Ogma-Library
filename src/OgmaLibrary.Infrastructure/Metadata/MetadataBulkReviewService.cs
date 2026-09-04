using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// Durable, atomic batch application and token-protected undo for metadata
/// proposals. Batch state is kept in the append-only audit stream so no second
/// command ledger can drift from the existing metadata audit boundary.
/// </summary>
public sealed class MetadataBulkReviewService : IMetadataBulkReviewService
{
    private const int MaximumProposalCount = 100;
    private const int MaximumAuditPayloadLength = 1_048_576;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly IMetadataQualityService _qualityService;

    /// <summary>Initializes the durable bulk-review command service.</summary>
    public MetadataBulkReviewService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IMetadataQualityService qualityService)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _qualityService = qualityService ?? throw new ArgumentNullException(nameof(qualityService));
    }

    /// <inheritdoc />
    public async Task<MetadataBulkReviewPreview> PreviewAsync(
        IReadOnlyList<long> proposalIds,
        CancellationToken cancellationToken = default)
    {
        long[] ids = NormalizeProposalIds(proposalIds);
        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<MetadataProposalRow> rows = await context.MetadataProposals
            .AsNoTracking()
            .Where(row => ids.Contains(row.MetadataProposalId) &&
                          row.Status == (int)MetadataProposalStatus.Pending)
            .OrderBy(row => row.MetadataProposalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count != ids.Length)
        {
            throw new InvalidOperationException(
                "Every selected metadata proposal must still be pending before a batch preview can be created.");
        }

        string[] bookIds = rows
            .Select(row => row.BookId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<BookMetadataFieldRow> fields = await context.BookMetadataFields
            .AsNoTracking()
            .Where(field => bookIds.Contains(field.BookId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<(string BookId, string FieldName), string?> currentValues = fields
            .GroupBy(field => (field.BookId, field.FieldName))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(field => field.IsOverridden)
                    .ThenByDescending(field => field.SourceTimestamp)
                    .Select(field => field.Value)
                    .FirstOrDefault());

        var items = rows.Select(row => new MetadataBulkReviewItem(
            row.MetadataProposalId,
            row.BookId,
            row.FieldName,
            currentValues.GetValueOrDefault((row.BookId, row.FieldName)),
            row.ProposedValue,
            row.Source,
            row.Confidence,
            row.Version)).ToList();

        return new MetadataBulkReviewPreview(
            CreateId("metabatch"),
            items,
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<MetadataBulkReviewResult> ApplyAsync(
        MetadataBulkReviewPreview preview,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        string actor = NormalizeActor(actorId);
        ValidatePreview(preview);

        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        MetadataProposalRow[] rows = await LoadAndValidateRowsAsync(context, preview, cancellationToken)
            .ConfigureAwait(false);
        string[] bookIds = rows.Select(row => row.BookId).Distinct(StringComparer.Ordinal).ToArray();
        List<BookSnapshot> before = await SnapshotAsync(context, bookIds, cancellationToken)
            .ConfigureAwait(false);

        foreach (IGrouping<string, MetadataProposalRow> group in rows.GroupBy(row => row.BookId, StringComparer.Ordinal))
        {
            await MetadataApplyService.ApplyMergedMetadataInContextAsync(
                context,
                group.Key,
                group.Select(row => new AcceptedFieldProposal(
                    row.FieldName,
                    row.ProposedValue,
                    row.Source,
                    row.Confidence,
                    IsOverridden: false)).ToArray(),
                cancellationToken).ConfigureAwait(false);

            foreach (MetadataProposalRow row in group)
            {
                row.Status = (int)MetadataProposalStatus.Accepted;
                row.DecidedUtc = DateTimeOffset.UtcNow;
                row.Version++;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        List<BookSnapshot> after = await SnapshotAsync(context, bookIds, cancellationToken)
            .ConfigureAwait(false);
        string undoToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var envelope = new BatchEnvelope(
            preview.BatchId,
            actor,
            HashToken(undoToken),
            before,
            after,
            rows.Select(row => new MetadataBulkDecisionResult(
                row.MetadataProposalId,
                row.BookId,
                row.FieldName,
                Applied: true,
                Error: null)).ToArray());
        string beforeJson = SerializeBounded(new BatchSnapshotEnvelope(preview.BatchId, before));
        string afterJson = SerializeBounded(envelope);

        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "MetadataBulkApplied",
            EntityId = preview.BatchId,
            EntityType = "MetadataReviewBatch",
            ActorId = actor,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        foreach (string bookId in bookIds)
        {
            await _qualityService.RecalculateAsync(bookId, cancellationToken).ConfigureAwait(false);
        }

        return new MetadataBulkReviewResult(
            preview.BatchId,
            undoToken,
            envelope.Decisions,
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<bool> UndoAsync(
        string batchId,
        string undoToken,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        string batch = NormalizeBatchId(batchId);
        string token = NormalizeToken(undoToken);
        string actor = NormalizeActor(actorId);

        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        List<AuditEventRow> audits = await context.AuditEvents
            .AsNoTracking()
            .Where(row => row.EventType == "MetadataBulkApplied" &&
                          row.EntityType == "MetadataReviewBatch" &&
                          row.EntityId == batch)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        AuditEventRow? audit = audits
            .OrderByDescending(row => row.Timestamp)
            .FirstOrDefault();
        if (audit?.AfterJson is null)
        {
            throw new KeyNotFoundException($"Metadata review batch '{batch}' was not found.");
        }

        BatchEnvelope envelope = DeserializeRequired<BatchEnvelope>(audit.AfterJson);
        byte[] expectedHash = Convert.FromHexString(envelope.UndoTokenHash);
        byte[] actualHash = Convert.FromHexString(HashToken(token));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
        {
            throw new UnauthorizedAccessException("The metadata batch undo token is invalid.");
        }

        bool alreadyUndone = await context.AuditEvents
            .AsNoTracking()
            .AnyAsync(row => row.EventType == "MetadataBulkUndone" &&
                             row.EntityType == "MetadataReviewBatch" &&
                             row.EntityId == batch,
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadyUndone)
        {
            return false;
        }

        string[] bookIds = envelope.After.Select(book => book.BookId).ToArray();
        List<BookSnapshot> current = await SnapshotAsync(context, bookIds, cancellationToken)
            .ConfigureAwait(false);
        if (!SnapshotsEqual(current, envelope.After))
        {
            throw new InvalidOperationException(
                "The metadata changed after this batch; reload and resolve the later edits before undoing it.");
        }

        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (BookSnapshot snapshot in envelope.Before)
        {
            await RestoreSnapshotAsync(context, snapshot, cancellationToken).ConfigureAwait(false);
        }

        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "MetadataBulkUndone",
            EntityId = batch,
            EntityType = "MetadataReviewBatch",
            ActorId = actor,
            BeforeJson = SerializeBounded(new BatchSnapshotEnvelope(batch, current)),
            AfterJson = SerializeBounded(new BatchSnapshotEnvelope(batch, envelope.Before)),
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        foreach (string bookId in bookIds)
        {
            await _qualityService.RecalculateAsync(bookId, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static long[] NormalizeProposalIds(IReadOnlyList<long> proposalIds)
    {
        ArgumentNullException.ThrowIfNull(proposalIds);
        long[] ids = proposalIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (ids.Length == 0 || ids.Length > MaximumProposalCount || ids.Length != proposalIds.Distinct().Count())
        {
            throw new ArgumentException(
                $"A bulk review must contain between 1 and {MaximumProposalCount} distinct positive proposal IDs.",
                nameof(proposalIds));
        }

        return ids;
    }

    private static void ValidatePreview(MetadataBulkReviewPreview preview)
    {
        string batchId = NormalizeBatchId(preview.BatchId);
        _ = batchId;
        if (preview.Items.Count == 0 || preview.Items.Count > MaximumProposalCount)
        {
            throw new ArgumentException("The metadata batch size is outside the supported range.", nameof(preview));
        }

        long[] ids = preview.Items.Select(item => item.ProposalId).ToArray();
        if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
        {
            throw new ArgumentException("Metadata batch proposal IDs must be distinct positive values.", nameof(preview));
        }
    }

    private static async Task<MetadataProposalRow[]> LoadAndValidateRowsAsync(
        CatalogueDbContext context,
        MetadataBulkReviewPreview preview,
        CancellationToken cancellationToken)
    {
        long[] ids = preview.Items.Select(item => item.ProposalId).ToArray();
        List<MetadataProposalRow> rows = await context.MetadataProposals
            .Where(row => ids.Contains(row.MetadataProposalId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count != ids.Length)
        {
            throw new InvalidOperationException("One or more metadata proposals no longer exist.");
        }

        Dictionary<long, MetadataBulkReviewItem> items = preview.Items.ToDictionary(item => item.ProposalId);
        foreach (MetadataProposalRow row in rows)
        {
            MetadataBulkReviewItem item = items[row.MetadataProposalId];
            if (row.Status != (int)MetadataProposalStatus.Pending ||
                row.Version != item.Version ||
                !string.Equals(row.BookId, item.BookId, StringComparison.Ordinal) ||
                !string.Equals(row.FieldName, item.FieldName, StringComparison.Ordinal) ||
                !string.Equals(row.ProposedValue, item.ProposedValue, StringComparison.Ordinal) ||
                !string.Equals(row.Source, item.Source, StringComparison.Ordinal) ||
                row.Confidence != item.Confidence)
            {
                throw new InvalidOperationException(
                    "A metadata proposal changed after the preview; create a fresh preview before applying it.");
            }
        }

        return rows.OrderBy(row => row.MetadataProposalId).ToArray();
    }

    private static async Task<List<BookSnapshot>> SnapshotAsync(
        CatalogueDbContext context,
        string[] bookIds,
        CancellationToken cancellationToken)
    {
        List<BookRow> books = await context.Books
            .AsNoTracking()
            .Where(book => bookIds.Contains(book.BookId))
            .Include(book => book.MetadataFields)
            .Include(book => book.BookAuthors)
            .ThenInclude(link => link.Author)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (books.Count != bookIds.Length)
        {
            throw new KeyNotFoundException("One or more books in the metadata batch no longer exist.");
        }

        return books
            .OrderBy(book => book.BookId, StringComparer.Ordinal)
            .Select(book => new BookSnapshot(
                book.BookId,
                book.Title,
                book.Year,
                book.IsbnNormalized,
                book.MetadataFields
                    .OrderBy(field => field.FieldName, StringComparer.Ordinal)
                    .ThenBy(field => field.Source, StringComparer.Ordinal)
                    .ThenBy(field => field.Value, StringComparer.Ordinal)
                    .Select(field => new MetadataFieldSnapshot(
                        field.FieldName,
                        field.Value,
                        field.Source,
                        field.SourceTimestamp,
                        field.Confidence,
                        field.IsOverridden))
                    .ToList(),
                book.BookAuthors
                    .OrderBy(link => link.DisplayOrder)
                    .ThenBy(link => link.Author!.NormalizedName, StringComparer.Ordinal)
                    .Select(link => new AuthorSnapshot(
                        link.Author!.NormalizedName,
                        link.Author.SortName,
                        link.Role,
                        link.DisplayOrder))
                    .ToList()))
            .ToList();
    }

    private static async Task RestoreSnapshotAsync(
        CatalogueDbContext context,
        BookSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        BookRow book = await context.Books
            .Include(row => row.MetadataFields)
            .Include(row => row.BookAuthors)
            .FirstAsync(row => row.BookId == snapshot.BookId, cancellationToken)
            .ConfigureAwait(false);
        context.BookMetadataFields.RemoveRange(book.MetadataFields);
        book.MetadataFields.Clear();
        foreach (MetadataFieldSnapshot field in snapshot.Fields)
        {
            book.MetadataFields.Add(new BookMetadataFieldRow
            {
                BookId = snapshot.BookId,
                FieldName = field.FieldName,
                Value = field.Value,
                Source = field.Source,
                SourceTimestamp = field.SourceTimestamp,
                Confidence = field.Confidence,
                IsOverridden = field.IsOverridden,
            });
        }

        context.BookAuthors.RemoveRange(book.BookAuthors);
        book.BookAuthors.Clear();
        foreach (AuthorSnapshot authorSnapshot in snapshot.Authors)
        {
            AuthorRow? author = await context.Authors
                .FirstOrDefaultAsync(row => row.NormalizedName == authorSnapshot.NormalizedName, cancellationToken)
                .ConfigureAwait(false);
            if (author is null)
            {
                author = new AuthorRow
                {
                    NormalizedName = authorSnapshot.NormalizedName,
                    SortName = authorSnapshot.SortName,
                };
                context.Authors.Add(author);
            }

            book.BookAuthors.Add(new BookAuthorRow
            {
                BookId = snapshot.BookId,
                Author = author,
                Role = authorSnapshot.Role,
                DisplayOrder = authorSnapshot.DisplayOrder,
            });
        }

        book.Title = snapshot.Title;
        book.Year = snapshot.Year;
        book.IsbnNormalized = snapshot.IsbnNormalized;
    }

    private static bool SnapshotsEqual(IReadOnlyList<BookSnapshot> left, IReadOnlyList<BookSnapshot> right) =>
        string.Equals(
            JsonSerializer.Serialize(left.OrderBy(snapshot => snapshot.BookId, StringComparer.Ordinal), JsonOptions),
            JsonSerializer.Serialize(right.OrderBy(snapshot => snapshot.BookId, StringComparer.Ordinal), JsonOptions),
            StringComparison.Ordinal);

    private static string NormalizeBatchId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentException("Metadata batch identifiers are limited to 128 characters.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > 256)
        {
            throw new ArgumentException("Metadata undo tokens are limited to 256 characters.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeActor(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > 256)
        {
            throw new ArgumentException("Metadata actor identifiers are limited to 256 characters.", nameof(value));
        }

        return normalized;
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string SerializeBounded<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        if (json.Length > MaximumAuditPayloadLength)
        {
            throw new InvalidOperationException("The metadata batch audit snapshot exceeds the storage safety limit.");
        }

        return json;
    }

    private static T DeserializeRequired<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidDataException("The metadata batch audit snapshot is invalid.");

    private static string CreateId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private sealed record BatchSnapshotEnvelope(string BatchId, IReadOnlyList<BookSnapshot> Books);

    private sealed record BatchEnvelope(
        string BatchId,
        string ActorId,
        string UndoTokenHash,
        IReadOnlyList<BookSnapshot> Before,
        IReadOnlyList<BookSnapshot> After,
        IReadOnlyList<MetadataBulkDecisionResult> Decisions);

    private sealed record BookSnapshot(
        string BookId,
        string? Title,
        int? Year,
        string? IsbnNormalized,
        IReadOnlyList<MetadataFieldSnapshot> Fields,
        IReadOnlyList<AuthorSnapshot> Authors);

    private sealed record MetadataFieldSnapshot(
        string FieldName,
        string? Value,
        string? Source,
        DateTimeOffset? SourceTimestamp,
        double? Confidence,
        bool IsOverridden);

    private sealed record AuthorSnapshot(
        string NormalizedName,
        string? SortName,
        string? Role,
        int DisplayOrder);
}
