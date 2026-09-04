using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// EF Core implementation of <see cref="IMetadataQualityService"/> (FR-META-007).
/// </summary>
/// <remarks>
/// <para>
/// Quality score formula:
/// <code>
/// QualityScore(book) = (fieldPresenceScore × 0.6) + (averageConfidence × 0.4)
/// fieldPresenceScore = (present core fields) / 7
/// Core fields: Title, Author, ISBN, Year, Publisher, Cover, Description
/// </code>
/// </para>
/// </remarks>
public sealed class MetadataQualityService : IMetadataQualityService
{
    private static readonly IReadOnlyList<string> CoreFields =
    [
        "Title",
        "Author",
        "ISBN",
        "Year",
        "Publisher",
        "Cover",
        "Description",
    ];

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataQualityService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal MetadataQualityService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataQualityService"/>.
    /// </summary>
    public MetadataQualityService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<BookQualityScore> RecalculateAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var book = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        var fields = await context.BookMetadataFields
            .AsNoTracking()
            .Where(f => f.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fieldMap = fields.ToDictionary(
            f => f.FieldName,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        // Also check the Books row directly for ISBN and Title.
        // A book may have these on the row even without a BookMetadataFieldRow.
        if (book is not null)
        {
            if (!fieldMap.ContainsKey("ISBN") && !string.IsNullOrWhiteSpace(book.IsbnNormalized))
            {
                fieldMap["ISBN"] = new Catalogue.Entities.BookMetadataFieldRow
                {
                    FieldName = "ISBN",
                    Value = book.IsbnNormalized,
                    Confidence = 0.5,
                };
            }

            if (!fieldMap.ContainsKey("Title") && !string.IsNullOrWhiteSpace(book.Title))
            {
                fieldMap["Title"] = new Catalogue.Entities.BookMetadataFieldRow
                {
                    FieldName = "Title",
                    Value = book.Title,
                    Confidence = 0.5,
                };
            }
        }

        int presentCount = 0;
        double totalConfidence = 0.0;
        int confidenceCount = 0;

        foreach (string field in CoreFields)
        {
            if (fieldMap.TryGetValue(field, out var row) && !string.IsNullOrWhiteSpace(row.Value))
            {
                presentCount++;
                if (row.Confidence.HasValue && row.Confidence.Value > 0.0)
                {
                    totalConfidence += row.Confidence.Value;
                    confidenceCount++;
                }
            }
        }

        double fieldPresenceScore = (double)presentCount / CoreFields.Count;
        double averageConfidence = confidenceCount > 0 ? totalConfidence / confidenceCount : 0.0;
        double qualityScore = (fieldPresenceScore * 0.6) + (averageConfidence * 0.4);

        // Clamp to [0, 1].
        qualityScore = Math.Clamp(qualityScore, 0.0, 1.0);

        if (book is not null)
        {
            book.QualityScore = qualityScore;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new BookQualityScore(
            BookId: bookId,
            QualityScore: qualityScore,
            PresentFieldCount: presentCount,
            TotalCoreFields: CoreFields.Count,
            AverageConfidence: averageConfidence);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BookSummaryProjection> GetBelowThresholdAsync(
        double maxScore,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var results = await context.Books
            .AsNoTracking()
            .Where(b => b.QualityScore <= maxScore)
            .OrderBy(b => b.QualityScore)
            .ThenBy(b => b.Title ?? string.Empty)
            .Select(b => new
            {
                b.BookId,
                b.Title,
                b.Status,
                b.Rating,
                b.Year,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                ShelfIds = b.ShelfBooks
                    .Select(sb => sb.ShelfId)
                    .ToList(),
                PrimaryRelativePath = b.BookFiles
                    .OrderBy(f => f.RelativePath)
                    .Select(f => f.RelativePath)
                    .FirstOrDefault(),
                Progress = b.ReadingProgress,
                HasPresentFile = b.BookFiles.Any(f => f.FileStatus == 0),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var item in results)
        {
            yield return new BookSummaryProjection(
                BookId: item.BookId,
                Title: item.Title,
                Authors: item.Authors,
                CoverRelativePath: null,
                Status: item.Status,
                Rating: item.Rating,
                ShelfIds: item.ShelfIds,
                ReadingProgressPct: item.Progress?.CompletionPct,
                IsAvailable: item.HasPresentFile,
                Year: item.Year,
                RelativePath: item.PrimaryRelativePath);
        }
    }
}
