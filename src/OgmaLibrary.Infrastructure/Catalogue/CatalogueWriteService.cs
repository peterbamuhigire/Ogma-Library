using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// EF Core implementation of <see cref="ICatalogueWriteService"/>.
/// All shelf CRUD, book-shelf assignments, metadata field edits, and bulk edits
/// flow through this class so the Bookshelf Presentation context never writes
/// directly to <see cref="CatalogueDbContext"/> (HLD §2.2).
/// </summary>
public sealed class CatalogueWriteService : ICatalogueWriteService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IAuditRepository _audit;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueWriteService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="audit">The audit repository.</param>
    internal CatalogueWriteService(CatalogueDbContext context, IAuditRepository audit)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(audit);
        _context = context;
        _audit = audit;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueWriteService"/>.
    /// </summary>
    public CatalogueWriteService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IAuditRepository audit)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(audit);
        _contextFactory = contextFactory;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<string> CreateShelfAsync(
        string name,
        bool isSmart = false,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (isSmart && !SmartShelfQueryParser.TryParse(query, out _))
        {
            throw new ArgumentException(
                "Smart shelf queries must be a valid JSON array of supported conditions.",
                nameof(query));
        }

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string id = Guid.NewGuid().ToString("N");
        var row = new ShelfRow
        {
            ShelfId = id,
            Name = name,
            ShelfType = isSmart ? 1 : 0,
            Query = isSmart ? query : null,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        context.Shelves.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc />
    public async Task RenameShelfAsync(
        string shelfId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ShelfRow? row = await context.Shelves
            .FirstOrDefaultAsync(s => s.ShelfId == shelfId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        row.Name = newName;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ShelfRow? row = await context.Shelves
            .Include(s => s.ShelfBooks)
            .FirstOrDefaultAsync(s => s.ShelfId == shelfId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        context.Shelves.Remove(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddBookToShelfAsync(
        string shelfId,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        bool already = await context.ShelfBooks
            .AnyAsync(sb => sb.ShelfId == shelfId && sb.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (already)
        {
            return;
        }

        context.ShelfBooks.Add(new ShelfBookRow
        {
            ShelfId = shelfId,
            BookId = bookId,
            AddedUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveBookFromShelfAsync(
        string shelfId,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        ShelfBookRow? row = await context.ShelfBooks
            .FirstOrDefaultAsync(sb => sb.ShelfId == shelfId && sb.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        context.ShelfBooks.Remove(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateMetadataFieldAsync(
        string bookId,
        string fieldName,
        string? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        BulkEditFieldRules.ValidateMetadataField(fieldName, value);

        if (TryGetEditableField(fieldName, out string? editableField))
        {
            await UpdateEditableFieldAsync(bookId, editableField, value, cancellationToken).ConfigureAwait(false);
            return;
        }

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var fieldRow = await context.BookMetadataFields
            .FirstOrDefaultAsync(f => f.BookId == bookId && f.FieldName == fieldName, cancellationToken)
            .ConfigureAwait(false);

        string? oldValue = fieldRow?.Value;

        if (fieldRow is null)
        {
            context.BookMetadataFields.Add(new BookMetadataFieldRow
            {
                BookId = bookId,
                FieldName = fieldName,
                Value = value,
                Source = "User",
                IsOverridden = true,
            });
        }
        else
        {
            fieldRow.Value = value;
            fieldRow.IsOverridden = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "MetadataEdit",
            EntityId = bookId,
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = value,
            }),
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.BookIds.Count == 0)
        {
            return;
        }

        string[] tagsToAdd = NormalizeTags(command.TagsToAdd, nameof(command.TagsToAdd));
        string[] tagsToRemove = NormalizeTags(command.TagsToRemove, nameof(command.TagsToRemove));

        // Sept-23 Phase 10 (10.8): the same rules as single-field edits and the preview.
        IReadOnlyList<string> authorsToSet = [];
        if (command.AuthorToSet is not null &&
            !BulkEditFieldRules.TryParseAuthors(command.AuthorToSet, out authorsToSet))
        {
            throw new ArgumentException("Author must hold one to twelve names of at most 256 characters each.", nameof(command));
        }

        if (command.YearToSet is { } yearToSet && !BulkEditFieldRules.IsValidYear(yearToSet))
        {
            throw new ArgumentException("Year must be a whole number from 1 to 9999.", nameof(command));
        }

        if (command.NewRating is { } newRating && newRating is < 1 or > 5)
        {
            throw new ArgumentException("Rating must be from 1 to 5.", nameof(command));
        }

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Capture before-state for audit.
        var beforeBooks = await context.Books
            .AsNoTracking()
            .Where(b => command.BookIds.Contains(b.BookId))
            .Select(b => new
            {
                b.BookId,
                b.Status,
                b.Rating,
                Tags = b.MetadataFields
                    .Where(field => field.FieldName == "Tag" || field.FieldName == "Tags")
                    .Select(field => field.Value)
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tagsToAdd.Length > 0 || tagsToRemove.Length > 0)
        {
            await ApplyTagsAsync(
                context,
                command.BookIds,
                tagsToAdd,
                tagsToRemove,
                cancellationToken).ConfigureAwait(false);
        }

        // Apply lifecycle status, rating, year and author overrides.
        if (command.NewLifecycleStatus.HasValue || command.NewRating.HasValue ||
            command.YearToSet.HasValue || command.AuthorToSet is not null)
        {
            var books = await context.Books
                .Include(b => b.BookAuthors)
                .Where(b => command.BookIds.Contains(b.BookId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var authorCache = new Dictionary<string, AuthorRow>(StringComparer.Ordinal);

            foreach (var book in books)
            {
                if (command.NewLifecycleStatus.HasValue)
                {
                    book.Status = command.NewLifecycleStatus.Value;
                }

                if (command.NewRating.HasValue)
                {
                    book.Rating = command.NewRating.Value;
                }

                if (command.YearToSet.HasValue)
                {
                    book.Year = command.YearToSet.Value;
                }

                if (command.AuthorToSet is not null)
                {
                    await SetAuthorsAsync(context, book, authorsToSet, authorCache, cancellationToken).ConfigureAwait(false);
                    await SetUserFieldAsync(context, book.BookId, "Author", hasValue: true, string.Join("; ", authorsToSet), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        // Apply shelf additions.
        if (command.ShelfIdToAdd is not null)
        {
            foreach (string bookId in command.BookIds)
            {
                bool exists = await context.ShelfBooks
                    .AnyAsync(sb => sb.ShelfId == command.ShelfIdToAdd && sb.BookId == bookId, cancellationToken)
                    .ConfigureAwait(false);

                if (!exists)
                {
                    context.ShelfBooks.Add(new ShelfBookRow
                    {
                        ShelfId = command.ShelfIdToAdd,
                        BookId = bookId,
                        AddedUtc = DateTimeOffset.UtcNow,
                    });
                }
            }
        }

        // Apply shelf removals.
        if (command.ShelfIdToRemove is not null)
        {
            var toRemove = await context.ShelfBooks
                .Where(sb => sb.ShelfId == command.ShelfIdToRemove && command.BookIds.Contains(sb.BookId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.ShelfBooks.RemoveRange(toRemove);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Record audit snapshot.
        var afterBooks = await context.Books
            .AsNoTracking()
            .Where(b => command.BookIds.Contains(b.BookId))
            .Select(b => new
            {
                b.BookId,
                b.Status,
                b.Rating,
                Tags = b.MetadataFields
                    .Where(field => field.FieldName == "Tag" || field.FieldName == "Tags")
                    .Select(field => field.Value)
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await _audit.AppendAsync(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "BulkEdit",
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                BookIds = command.BookIds,
                Before = beforeBooks,
                After = afterBooks,
            }),
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookIds);
        if (bookIds.Count == 0)
        {
            return [];
        }

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var rows = await context.Books
            .AsNoTracking()
            .Where(b => bookIds.Contains(b.BookId))
            .Select(b => new
            {
                b.BookId,
                b.Year,
                b.Status,
                b.Rating,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                UserFields = b.MetadataFields
                    .Where(f => f.Source == "User" && (f.FieldName == "Author" || f.FieldName == "Tags"))
                    .Select(f => new { f.FieldName, f.Value })
                    .ToList(),
                ManualShelfIds = b.ShelfBooks
                    .Where(sb => sb.Shelf!.ShelfType == 0)
                    .Select(sb => sb.ShelfId)
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row =>
            {
                var author = row.UserFields.FirstOrDefault(f => f.FieldName == "Author");
                var tags = row.UserFields.FirstOrDefault(f => f.FieldName == "Tags");
                return new BulkEditSnapshot(
                    row.BookId,
                    row.Authors,
                    author?.Value,
                    author is not null,
                    row.Year,
                    row.Status,
                    row.Rating,
                    tags?.Value,
                    tags is not null,
                    row.ManualShelfIds);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task RestoreBulkEditSnapshotAsync(
        IReadOnlyList<BulkEditSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0)
        {
            return;
        }

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string[] ids = snapshots.Select(s => s.BookId).Distinct(StringComparer.Ordinal).ToArray();
        Dictionary<string, BookRow> books = await context.Books
            .Include(b => b.BookAuthors)
            .Include(b => b.ShelfBooks)
            .Where(b => ids.Contains(b.BookId))
            .ToDictionaryAsync(b => b.BookId, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> manualShelves = (await context.Shelves
                .Where(s => s.ShelfType == 0)
                .Select(s => s.ShelfId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        var authorCache = new Dictionary<string, AuthorRow>(StringComparer.Ordinal);

        foreach (BulkEditSnapshot snapshot in snapshots)
        {
            if (!books.TryGetValue(snapshot.BookId, out BookRow? book))
            {
                continue;
            }

            book.Year = snapshot.Year;
            book.Status = snapshot.LifecycleStatus;
            book.Rating = snapshot.Rating;
            await SetAuthorsAsync(context, book, snapshot.Authors, authorCache, cancellationToken).ConfigureAwait(false);
            await SetUserFieldAsync(context, book.BookId, "Author", snapshot.HasUserAuthorField, snapshot.UserAuthorField, cancellationToken)
                .ConfigureAwait(false);
            await SetUserFieldAsync(context, book.BookId, "Tags", snapshot.HasUserTags, snapshot.UserTags, cancellationToken)
                .ConfigureAwait(false);

            HashSet<string> wanted = snapshot.ManualShelfIds
                .Where(manualShelves.Contains)
                .ToHashSet(StringComparer.Ordinal);
            foreach (ShelfBookRow membership in book.ShelfBooks
                         .Where(sb => manualShelves.Contains(sb.ShelfId) && !wanted.Contains(sb.ShelfId))
                         .ToList())
            {
                context.ShelfBooks.Remove(membership);
            }

            foreach (string shelfId in wanted.Where(id => !book.ShelfBooks.Any(sb => sb.ShelfId == id)))
            {
                context.ShelfBooks.Add(new ShelfBookRow
                {
                    ShelfId = shelfId,
                    BookId = book.BookId,
                    AddedUtc = DateTimeOffset.UtcNow,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "BulkEditUndo",
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(new { BookIds = ids }),
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MetadataFieldSnapshot?> CaptureMetadataFieldSnapshotAsync(
        string bookId,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        string field = TryGetEditableField(fieldName, out string? editable) ? editable : fieldName;

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        var row = await context.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new
            {
                b.Title,
                b.Year,
                b.IsbnNormalized,
                b.Doi,
                Authors = b.BookAuthors
                    .OrderBy(ba => ba.DisplayOrder)
                    .Select(ba => ba.Author!.NormalizedName)
                    .ToList(),
                UserFields = b.MetadataFields
                    .Where(f => f.Source == "User" && f.FieldName == field)
                    .Select(f => f.Value)
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        string? canonical = field switch
        {
            EditableMetadataFields.Title => row.Title,
            EditableMetadataFields.Year => row.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EditableMetadataFields.Isbn => row.IsbnNormalized,
            EditableMetadataFields.Doi => row.Doi,
            _ => null,
        };

        return new MetadataFieldSnapshot(
            bookId,
            field,
            canonical,
            row.Authors,
            row.UserFields.Count > 0,
            row.UserFields.FirstOrDefault());
    }

    /// <inheritdoc />
    public async Task RestoreMetadataFieldSnapshotAsync(
        MetadataFieldSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? book = await context.Books
            .Include(b => b.BookAuthors)
            .ThenInclude(ba => ba.Author)
            .FirstOrDefaultAsync(b => b.BookId == snapshot.BookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return;
        }

        switch (snapshot.FieldName)
        {
            case EditableMetadataFields.Title:
                book.Title = snapshot.CanonicalValue;
                break;
            case EditableMetadataFields.Year:
                book.Year = snapshot.CanonicalValue is null
                    ? null
                    : int.Parse(snapshot.CanonicalValue, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case EditableMetadataFields.Isbn:
                book.IsbnNormalized = snapshot.CanonicalValue;
                break;
            case EditableMetadataFields.Doi:
                book.Doi = snapshot.CanonicalValue;
                break;
            case EditableMetadataFields.Author:
                await SetAuthorsAsync(context, book, snapshot.Authors, new Dictionary<string, AuthorRow>(StringComparer.Ordinal), cancellationToken)
                    .ConfigureAwait(false);
                break;
        }

        await SetUserFieldAsync(context, book.BookId, snapshot.FieldName, snapshot.HasUserField, snapshot.UserFieldValue, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "MetadataEditUndo",
            EntityId = book.BookId,
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(new { snapshot.FieldName }),
        }, cancellationToken).ConfigureAwait(false);
    }

    // Writes one inspector-editable field (Sept-23 Phase 11, task 11.4, D-63): the value goes into
    // the user-entered row only, so provider rows keep their provenance, and is mirrored into the
    // canonical column the read model shows.
    private async Task UpdateEditableFieldAsync(
        string bookId,
        string fieldName,
        string? value,
        CancellationToken cancellationToken)
    {
        MetadataFieldEditError error = MetadataFieldEditRules.TryNormalize(fieldName, value, out string? normalized);
        if (error != MetadataFieldEditError.None)
        {
            throw new ArgumentException($"The value is not a valid {fieldName} ({error}).", nameof(value));
        }

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow book = await context.Books
            .Include(b => b.BookAuthors)
            .ThenInclude(ba => ba.Author)
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The book no longer exists in the catalogue.");

        string? oldValue = fieldName switch
        {
            EditableMetadataFields.Title => book.Title,
            EditableMetadataFields.Year => book.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EditableMetadataFields.Isbn => book.IsbnNormalized,
            EditableMetadataFields.Doi => book.Doi,
            EditableMetadataFields.Author => BulkEditFieldRules.FormatAuthors(book.BookAuthors
                .OrderBy(ba => ba.DisplayOrder)
                .Select(ba => ba.Author?.NormalizedName ?? string.Empty)
                .ToList()),
            _ => null,
        };

        switch (fieldName)
        {
            case EditableMetadataFields.Title:
                book.Title = normalized;
                break;
            case EditableMetadataFields.Year:
                book.Year = normalized is null
                    ? null
                    : int.Parse(normalized, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case EditableMetadataFields.Isbn:
                book.IsbnNormalized = normalized;
                break;
            case EditableMetadataFields.Doi:
                book.Doi = normalized;
                break;
            case EditableMetadataFields.Author:
                IReadOnlyList<string> authors = [];
                if (normalized is not null && !BulkEditFieldRules.TryParseAuthors(normalized, out authors))
                {
                    throw new ArgumentException("The author list is not valid.", nameof(value));
                }

                await SetAuthorsAsync(context, book, authors, new Dictionary<string, AuthorRow>(StringComparer.Ordinal), cancellationToken)
                    .ConfigureAwait(false);
                break;
        }

        await SetUserFieldAsync(context, bookId, fieldName, hasValue: true, normalized, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "MetadataEdit",
            EntityId = bookId,
            TimestampUtc = DateTimeOffset.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = normalized,
            }),
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetEditableField(string fieldName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? canonical)
    {
        canonical = EditableMetadataFields.All.FirstOrDefault(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase));
        return canonical is not null;
    }

    // Replaces a book's linked authors with `names`, in order. Rows for authors that stay are
    // kept (the key is book and author), so no row is deleted and re-inserted in one save.
    private static async Task SetAuthorsAsync(
        CatalogueDbContext context,
        BookRow book,
        IReadOnlyList<string> names,
        Dictionary<string, AuthorRow> authorCache,
        CancellationToken cancellationToken)
    {
        var kept = new List<BookAuthorRow>();
        for (int order = 0; order < names.Count; order++)
        {
            string name = names[order];
            if (!authorCache.TryGetValue(name, out AuthorRow? author))
            {
                author = await context.Authors
                    .FirstOrDefaultAsync(a => a.NormalizedName == name, cancellationToken)
                    .ConfigureAwait(false);
                if (author is null)
                {
                    author = new AuthorRow { NormalizedName = name, SortName = BuildSortName(name) };
                    context.Authors.Add(author);
                }

                authorCache[name] = author;
            }

            BookAuthorRow? link = book.BookAuthors.FirstOrDefault(ba =>
                ReferenceEquals(ba.Author, author) || (author.AuthorId != 0 && ba.AuthorId == author.AuthorId));
            if (link is null)
            {
                link = new BookAuthorRow { BookId = book.BookId, Author = author, Role = "Author" };
                book.BookAuthors.Add(link);
            }

            link.DisplayOrder = order;
            kept.Add(link);
        }

        foreach (BookAuthorRow stale in book.BookAuthors.Where(ba => !kept.Contains(ba)).ToList())
        {
            book.BookAuthors.Remove(stale);
            context.BookAuthors.Remove(stale);
        }
    }

    // Upserts or deletes the user-entered metadata row for one field.
    private static async Task SetUserFieldAsync(
        CatalogueDbContext context,
        string bookId,
        string fieldName,
        bool hasValue,
        string? value,
        CancellationToken cancellationToken)
    {
        BookMetadataFieldRow? row = context.BookMetadataFields.Local.FirstOrDefault(f =>
                f.BookId == bookId && f.FieldName == fieldName && f.Source == "User") ??
            await context.BookMetadataFields
                .FirstOrDefaultAsync(f => f.BookId == bookId && f.FieldName == fieldName && f.Source == "User", cancellationToken)
                .ConfigureAwait(false);

        if (!hasValue)
        {
            if (row is not null)
            {
                context.BookMetadataFields.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new BookMetadataFieldRow { BookId = bookId, FieldName = fieldName, Source = "User" };
            context.BookMetadataFields.Add(row);
        }

        row.Value = value;
        row.IsOverridden = true;
        row.SourceTimestamp = DateTimeOffset.UtcNow;
    }

    private static string BuildSortName(string name)
    {
        int lastSpace = name.LastIndexOf(' ');
        return lastSpace <= 0 ? name : name[(lastSpace + 1)..] + ", " + name[..lastSpace];
    }

    private static async Task ApplyTagsAsync(
        CatalogueDbContext context,
        IReadOnlyList<string> bookIds,
        IReadOnlyList<string> tagsToAdd,
        IReadOnlyList<string> tagsToRemove,
        CancellationToken cancellationToken)
    {
        List<BookMetadataFieldRow> tagFields = await context.BookMetadataFields
            .Where(field => bookIds.Contains(field.BookId) &&
                            (field.FieldName == "Tag" || field.FieldName == "Tags"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> remove = tagsToRemove.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string bookId in bookIds.Distinct(StringComparer.Ordinal))
        {
            List<BookMetadataFieldRow> bookFields = tagFields
                .Where(field => string.Equals(field.BookId, bookId, StringComparison.Ordinal))
                .ToList();
            string[] current = bookFields
                .SelectMany(field => ParseTags(field.Value))
                .Where(tag => !remove.Contains(tag))
                .Concat(tagsToAdd)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string serialized = string.Join("; ", current);

            BookMetadataFieldRow? userTags = bookFields
                .FirstOrDefault(field => field.FieldName == "Tags" &&
                                         string.Equals(field.Source, "User", StringComparison.Ordinal));
            if (userTags is null)
            {
                userTags = new BookMetadataFieldRow
                {
                    BookId = bookId,
                    FieldName = "Tags",
                    Source = "User",
                };
                context.BookMetadataFields.Add(userTags);
            }

            userTags.Value = string.IsNullOrWhiteSpace(serialized) ? null : serialized;
            userTags.IsOverridden = true;
            userTags.SourceTimestamp = DateTimeOffset.UtcNow;
        }
    }

    private static string[] NormalizeTags(IReadOnlyList<string> tags, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count > 32)
        {
            throw new ArgumentException("A bulk edit may contain at most 32 tags per operation.", parameterName);
        }

        string[] normalized = tags
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Any(tag => tag.Length > 128 || tag.Contains(';') || tag.Contains('|')))
        {
            throw new ArgumentException("Tags must be non-empty, at most 128 characters, and contain no delimiters.", parameterName);
        }

        return normalized;
    }

    private static IEnumerable<string> ParseTags(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0);
}
