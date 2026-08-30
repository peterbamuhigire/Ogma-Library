using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// EF Core implementation of <see cref="IMetadataApplyService"/> that upserts
/// <c>BookMetadataFields</c> rows with full provenance, writes an audit event,
/// and delegates quality-score recalculation (FR-META-003, FR-META-004,
/// CTRL-OGMA-018).
/// </summary>
public sealed class MetadataApplyService : IMetadataApplyService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IMetadataQualityService _qualityService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataApplyService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="qualityService">The quality score service.</param>
    internal MetadataApplyService(
        CatalogueDbContext context,
        IMetadataQualityService qualityService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(qualityService);
        _context = context;
        _qualityService = qualityService;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataApplyService"/>.
    /// </summary>
    public MetadataApplyService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IMetadataQualityService qualityService)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(qualityService);
        _contextFactory = contextFactory;
        _qualityService = qualityService;
    }

    /// <inheritdoc />
    public async Task ApplyMergedMetadataAsync(
        string bookId,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(acceptedProposals);

        if (acceptedProposals.Count == 0)
        {
            return;
        }

        ValidateProposals(acceptedProposals);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Snapshot before state for audit.
        var beforeFields = await context.BookMetadataFields
            .AsNoTracking()
            .Where(f => f.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string beforeJson = JsonSerializer.Serialize(
            beforeFields.Select(f => new { f.FieldName, f.Value, f.Source, f.Confidence }));

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (AcceptedFieldProposal proposal in acceptedProposals)
        {
            var existing = await context.BookMetadataFields
                .FirstOrDefaultAsync(
                    f => f.BookId == bookId && f.FieldName == proposal.FieldName,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                context.BookMetadataFields.Add(new BookMetadataFieldRow
                {
                    BookId = bookId,
                    FieldName = proposal.FieldName,
                    Value = proposal.AcceptedValue,
                    Source = proposal.Source,
                    SourceTimestamp = now,
                    Confidence = proposal.Confidence,
                    IsOverridden = proposal.IsOverridden,
                });
            }
            else
            {
                // A user override is authoritative until the user explicitly
                // submits another override for the same field.
                if (existing.IsOverridden && !proposal.IsOverridden)
                {
                    continue;
                }

                existing.Value = proposal.AcceptedValue;
                existing.Source = proposal.Source;
                existing.SourceTimestamp = now;
                existing.Confidence = proposal.Confidence;
                existing.IsOverridden = proposal.IsOverridden;
            }
        }

        await MirrorCatalogueColumnsAsync(context, bookId, acceptedProposals, cancellationToken)
            .ConfigureAwait(false);

        // Snapshot after state.
        string afterJson = JsonSerializer.Serialize(
            acceptedProposals.Select(p => new { p.FieldName, p.AcceptedValue, p.Source, p.Confidence }));

        // Audit event.
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "MetadataApplied",
            EntityId = bookId,
            EntityType = "Book",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            Timestamp = now,
            IsLocalOnly = true,
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Recalculate quality score after metadata write.
        await _qualityService.RecalculateAsync(bookId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task MirrorCatalogueColumnsAsync(
        CatalogueDbContext context,
        string bookId,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        CancellationToken cancellationToken)
    {
        BookRow? book = await context.Books
            .Include(b => b.BookAuthors)
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (book is null)
        {
            return;
        }

        string? title = FindAcceptedValue(acceptedProposals, "Title");
        if (!string.IsNullOrWhiteSpace(title))
        {
            book.Title = title.Trim();
        }

        string? year = FindAcceptedValue(acceptedProposals, "Year");
        if (int.TryParse(year, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsedYear))
        {
            book.Year = parsedYear;
        }

        string? isbn = FindAcceptedValue(acceptedProposals, "ISBN");
        if (Isbn.TryParse(isbn, out Isbn parsedIsbn))
        {
            book.IsbnNormalized = parsedIsbn.Normalized;
        }

        string? authorValue = FindAcceptedValue(acceptedProposals, "Author");
        if (!string.IsNullOrWhiteSpace(authorValue))
        {
            await ReplaceAuthorsAsync(context, book, authorValue, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ReplaceAuthorsAsync(
        CatalogueDbContext context,
        BookRow book,
        string authorValue,
        CancellationToken cancellationToken)
    {
        var authorNames = authorValue
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (authorNames.Count == 0)
        {
            return;
        }

        context.BookAuthors.RemoveRange(book.BookAuthors);
        book.BookAuthors.Clear();

        int displayOrder = 0;
        foreach (string authorName in authorNames)
        {
            string normalized = NormalizeAuthorName(authorName);
            AuthorRow? author = await context.Authors
                .FirstOrDefaultAsync(a => a.NormalizedName == normalized, cancellationToken)
                .ConfigureAwait(false);

            if (author is null)
            {
                author = new AuthorRow
                {
                    NormalizedName = normalized,
                    SortName = BuildSortName(authorName),
                };
                context.Authors.Add(author);
            }

            book.BookAuthors.Add(new BookAuthorRow
            {
                BookId = book.BookId,
                Author = author,
                Role = "Author",
                DisplayOrder = displayOrder++,
            });
        }
    }

    private static string? FindAcceptedValue(
        IReadOnlyList<AcceptedFieldProposal> proposals,
        string fieldName) =>
        proposals
            .FirstOrDefault(p => string.Equals(p.FieldName, fieldName, StringComparison.OrdinalIgnoreCase))
            ?.AcceptedValue;

    private static void ValidateProposals(IReadOnlyList<AcceptedFieldProposal> proposals)
    {
        foreach (AcceptedFieldProposal proposal in proposals)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(proposal.FieldName);
            ArgumentException.ThrowIfNullOrWhiteSpace(proposal.Source);
            if (proposal.FieldName.Length > 128 || proposal.Source.Length > 128)
            {
                throw new ArgumentException("Metadata field names and sources must be at most 128 characters.");
            }

            if (proposal.AcceptedValue?.Length > 4096)
            {
                throw new ArgumentException("Metadata values must be at most 4096 characters.");
            }

            if (proposal.Confidence is < 0.0 or > 1.0 || double.IsNaN(proposal.Confidence))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(proposals), "Metadata proposal confidence must be within [0.0, 1.0].");
            }

            if (proposal.IsOverridden &&
                (!string.Equals(proposal.Source, "UserOverride", StringComparison.Ordinal) ||
                 proposal.Confidence != 1.0))
            {
                throw new ArgumentException(
                    "User overrides must use source UserOverride and confidence 1.0.");
            }
        }
    }

    private static string NormalizeAuthorName(string authorName) =>
        string.Join(
            ' ',
            authorName.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();

    private static string BuildSortName(string authorName)
    {
        string normalized = NormalizeAuthorName(authorName);
        int lastSpace = normalized.LastIndexOf(' ');
        return lastSpace <= 0
            ? normalized
            : normalized[(lastSpace + 1)..] + ", " + normalized[..lastSpace];
    }
}
