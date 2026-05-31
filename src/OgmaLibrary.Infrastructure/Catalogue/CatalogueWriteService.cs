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
    private readonly CatalogueDbContext _context;
    private readonly IAuditRepository _audit;

    /// <summary>
    /// Initializes a new instance of <see cref="CatalogueWriteService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="audit">The audit repository.</param>
    public CatalogueWriteService(CatalogueDbContext context, IAuditRepository audit)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(audit);
        _context = context;
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

        string id = Guid.NewGuid().ToString("N");
        var row = new ShelfRow
        {
            ShelfId = id,
            Name = name,
            ShelfType = isSmart ? 1 : 0,
            Query = query,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        _context.Shelves.Add(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        ShelfRow? row = await _context.Shelves
            .FirstOrDefaultAsync(s => s.ShelfId == shelfId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        row.Name = newName;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);

        ShelfRow? row = await _context.Shelves
            .Include(s => s.ShelfBooks)
            .FirstOrDefaultAsync(s => s.ShelfId == shelfId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        _context.Shelves.Remove(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddBookToShelfAsync(
        string shelfId,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        bool already = await _context.ShelfBooks
            .AnyAsync(sb => sb.ShelfId == shelfId && sb.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (already)
        {
            return;
        }

        _context.ShelfBooks.Add(new ShelfBookRow
        {
            ShelfId = shelfId,
            BookId = bookId,
            AddedUtc = DateTimeOffset.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveBookFromShelfAsync(
        string shelfId,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        ShelfBookRow? row = await _context.ShelfBooks
            .FirstOrDefaultAsync(sb => sb.ShelfId == shelfId && sb.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        _context.ShelfBooks.Remove(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        var fieldRow = await _context.BookMetadataFields
            .FirstOrDefaultAsync(f => f.BookId == bookId && f.FieldName == fieldName, cancellationToken)
            .ConfigureAwait(false);

        string? oldValue = fieldRow?.Value;

        if (fieldRow is null)
        {
            _context.BookMetadataFields.Add(new BookMetadataFieldRow
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

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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

        // Capture before-state for audit.
        var beforeBooks = await _context.Books
            .AsNoTracking()
            .Where(b => command.BookIds.Contains(b.BookId))
            .Select(b => new { b.BookId, b.Status, b.Rating })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Apply status / rating overrides.
        if (command.NewStatus.HasValue || command.NewRating.HasValue)
        {
            var books = await _context.Books
                .Where(b => command.BookIds.Contains(b.BookId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var book in books)
            {
                if (command.NewStatus.HasValue)
                {
                    book.Status = command.NewStatus.Value;
                }

                if (command.NewRating.HasValue)
                {
                    book.Rating = command.NewRating.Value;
                }
            }
        }

        // Apply shelf additions.
        if (command.ShelfIdToAdd is not null)
        {
            foreach (string bookId in command.BookIds)
            {
                bool exists = await _context.ShelfBooks
                    .AnyAsync(sb => sb.ShelfId == command.ShelfIdToAdd && sb.BookId == bookId, cancellationToken)
                    .ConfigureAwait(false);

                if (!exists)
                {
                    _context.ShelfBooks.Add(new ShelfBookRow
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
            var toRemove = await _context.ShelfBooks
                .Where(sb => sb.ShelfId == command.ShelfIdToRemove && command.BookIds.Contains(sb.BookId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _context.ShelfBooks.RemoveRange(toRemove);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Record audit snapshot.
        var afterBooks = await _context.Books
            .AsNoTracking()
            .Where(b => command.BookIds.Contains(b.BookId))
            .Select(b => new { b.BookId, b.Status, b.Rating })
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
}
