using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>Outcome of promoting extracted ISBN evidence into canonical metadata.</summary>
public enum IsbnPromotionOutcome
{
    /// <summary>The book has no ISBN evidence.</summary>
    NoEvidence = 0,

    /// <summary>The best ISBN was promoted (or was already the canonical value).</summary>
    Promoted = 1,

    /// <summary>The best evidence failed checksum validation and was not promoted.</summary>
    Invalid = 2,

    /// <summary>A user override or another authoritative source already set the ISBN.</summary>
    KeptExisting = 3,

    /// <summary>The book no longer exists.</summary>
    BookMissing = 4,
}

/// <summary>
/// Promotes the best extracted ISBN into the catalogue (Sept-23 Phase 06, T06.8, K24) and
/// assigns every book an edition, grouping books that share an ISBN as one edition of one
/// work (T06.9, K23). The SQLite catalogue stays the single source of identity: extracted
/// evidence is re-validated (ISBN-10/13 checksum) before promotion; a user override
/// (<c>IsOverridden</c>) or a value applied from another source always wins; provenance is
/// recorded as <c>extracted:&lt;artifact&gt;</c>. Books are never merged by ISBN — only
/// grouped — so a user can split them later (Phase 10).
/// </summary>
public sealed class IsbnPromotionService
{
    /// <summary>The metadata field name used for ISBN values.</summary>
    public const string IsbnField = "ISBN";

    /// <summary>The provenance prefix for promoted extracted values.</summary>
    public const string ExtractedSourcePrefix = "extracted:";

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Runtime constructor using operation-scoped contexts.</summary>
    /// <param name="contextFactory">The catalogue context factory.</param>
    public IsbnPromotionService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>Test constructor using a shared context.</summary>
    /// <param name="context">The catalogue context.</param>
    internal IsbnPromotionService(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Promotes the best ISBN evidence for one book and assigns its edition.</summary>
    /// <param name="bookId">The book.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The promotion outcome.</returns>
    public async Task<IsbnPromotionOutcome> PromoteAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        CatalogueDbContext context = _context ?? await _contextFactory!
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            BookRow? book = await context.Books
                .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);
            if (book is null)
            {
                return IsbnPromotionOutcome.BookMissing;
            }

            IsbnPromotionOutcome outcome = await PromoteCoreAsync(context, book, cancellationToken)
                .ConfigureAwait(false);
            await AssignEditionAsync(context, book, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return outcome;
        }
        finally
        {
            if (_context is null)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Returns the ISBN-13 form used to group editions (an ISBN-10 is converted).</summary>
    /// <param name="normalized">A validated normalized ISBN.</param>
    /// <returns>The 13-digit grouping key.</returns>
    public static string ToIsbn13(string normalized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalized);
        if (normalized.Length == 13)
        {
            return normalized;
        }

        string body = "978" + normalized[..9];
        int sum = 0;
        for (int index = 0; index < 12; index++)
        {
            sum += (body[index] - '0') * (index % 2 == 0 ? 1 : 3);
        }

        int check = (10 - (sum % 10)) % 10;
        return body + check.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<IsbnPromotionOutcome> PromoteCoreAsync(
        CatalogueDbContext context,
        BookRow book,
        CancellationToken cancellationToken)
    {
        ExtractedIsbnEvidenceRow? best = await context.ExtractedIsbnEvidence
            .AsNoTracking()
            .Where(row => row.BookId == book.BookId && row.IsBest)
            .OrderByDescending(row => row.ExtractionArtifactId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (best is null)
        {
            return IsbnPromotionOutcome.NoEvidence;
        }

        if (!Isbn.TryParse(best.IsbnNormalized, out Isbn isbn))
        {
            return IsbnPromotionOutcome.Invalid;
        }

        List<BookMetadataFieldRow> isbnFields = await context.BookMetadataFields
            .Where(field => field.BookId == book.BookId && field.FieldName == IsbnField)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        bool authoritativeElsewhere = isbnFields.Any(field =>
            field.IsOverridden ||
            (field.Source is not null &&
             !field.Source.StartsWith(ExtractedSourcePrefix, StringComparison.Ordinal) &&
             !string.Equals(field.Source, "PDF", StringComparison.Ordinal)));
        if (authoritativeElsewhere)
        {
            return IsbnPromotionOutcome.KeptExisting;
        }

        string source = ExtractedSourcePrefix +
            best.ExtractionArtifactId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        BookMetadataFieldRow? extracted = isbnFields.FirstOrDefault(field =>
            field.Source?.StartsWith(ExtractedSourcePrefix, StringComparison.Ordinal) == true);
        if (extracted is null)
        {
            context.BookMetadataFields.Add(new BookMetadataFieldRow
            {
                BookId = book.BookId,
                FieldName = IsbnField,
                Value = isbn.Normalized,
                Source = source,
                SourceTimestamp = now,
                Confidence = 0.8,
            });
        }
        else
        {
            extracted.Value = isbn.Normalized;
            extracted.Source = source;
            extracted.SourceTimestamp = now;
        }

        string? before = book.IsbnNormalized;
        if (!string.Equals(before, isbn.Normalized, StringComparison.Ordinal))
        {
            book.IsbnNormalized = isbn.Normalized;
            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "IsbnPromoted",
                EntityId = book.BookId,
                EntityType = "Book",
                BeforeJson = JsonSerializer.Serialize(new { isbn = before }),
                AfterJson = JsonSerializer.Serialize(new { isbn = isbn.Normalized, source }),
                Timestamp = now,
                IsLocalOnly = true,
            });
        }

        return IsbnPromotionOutcome.Promoted;
    }

    private static async Task AssignEditionAsync(
        CatalogueDbContext context,
        BookRow book,
        CancellationToken cancellationToken)
    {
        string? groupKey = book.IsbnNormalized is { Length: > 0 } normalized
            ? ToIsbn13(normalized)
            : null;
        if (groupKey is not null)
        {
            // Another book with the same ISBN already has an edition: join it.
            List<BookRow> sameIsbn = await context.Books
                .Where(other => other.BookId != book.BookId &&
                                other.EditionId != null &&
                                other.IsbnNormalized != null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            BookRow? sibling = sameIsbn.FirstOrDefault(other =>
                string.Equals(ToIsbn13(other.IsbnNormalized!), groupKey, StringComparison.Ordinal));
            if (sibling is not null)
            {
                if (book.EditionId != sibling.EditionId)
                {
                    long? previous = book.EditionId;
                    book.EditionId = sibling.EditionId;
                    RecordDecision(context, book.BookId, previous, sibling.EditionId, "same_isbn", sibling.BookId);
                }

                return;
            }
        }

        if (book.EditionId is not null)
        {
            bool shared = await context.Books
                .AnyAsync(other => other.BookId != book.BookId && other.EditionId == book.EditionId, cancellationToken)
                .ConfigureAwait(false);
            if (!shared)
            {
                // The book already owns a singleton edition.
                return;
            }
        }

        // Every catalogued book belongs to an edition of a work (CAT-007), even before any
        // bibliographic evidence exists. A later same-ISBN book joins this edition.
        var work = new WorkRow { CanonicalTitle = book.Title };
        var edition = new EditionRow { Work = work, PublicationYear = book.Year };
        context.Works.Add(work);
        context.Editions.Add(edition);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        long? before = book.EditionId;
        book.EditionId = edition.EditionId;
        RecordDecision(context, book.BookId, before, edition.EditionId, groupKey is null ? "new_work" : "new_isbn", null);
    }

    private static void RecordDecision(
        CatalogueDbContext context,
        string bookId,
        long? before,
        long? after,
        string reason,
        string? matchedBookId)
    {
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "IdentityEditionAssigned",
            EntityId = bookId,
            EntityType = "Book",
            BeforeJson = JsonSerializer.Serialize(new { editionId = before }),
            AfterJson = JsonSerializer.Serialize(new { editionId = after, reason, matchedBookId }),
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
    }
}
