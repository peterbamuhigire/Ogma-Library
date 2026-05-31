using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IReadingMemoryRepository"/> against
/// <see cref="CatalogueDbContext"/> (Phase 09 world-class addition).
/// </summary>
public sealed class ReadingMemoryRepository : IReadingMemoryRepository
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="ReadingMemoryRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public ReadingMemoryRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ReadingMemory?> GetForBookAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        ReadingMemoryRow? row = await _context.ReadingMemory
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task SaveAsync(ReadingMemory memory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memory);

        ReadingMemoryRow? existing = await _context.ReadingMemory
            .FirstOrDefaultAsync(m => m.BookId == memory.BookId, cancellationToken)
            .ConfigureAwait(false);

        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues? originalValues = existing is null
            ? null
            : _context.Entry(existing).OriginalValues.Clone();
        ReadingMemoryRow? addedRow = null;

        var tx = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (tx.ConfigureAwait(false))
        {
            try
            {
                if (existing is null)
                {
                    addedRow = new ReadingMemoryRow
                    {
                        BookId = memory.BookId,
                        OpenedBecause = memory.OpenedBecause,
                        KeyInsight = memory.KeyInsight,
                        OpenQuestions = memory.OpenQuestions,
                        Disposition = memory.Disposition,
                        CreatedAtUtc = memory.CreatedAtUtc,
                        UpdatedAtUtc = memory.UpdatedAtUtc,
                    };
                    _context.ReadingMemory.Add(addedRow);
                }
                else
                {
                    existing.OpenedBecause = memory.OpenedBecause;
                    existing.KeyInsight = memory.KeyInsight;
                    existing.OpenQuestions = memory.OpenQuestions;
                    existing.Disposition = memory.Disposition;
                    existing.UpdatedAtUtc = memory.UpdatedAtUtc;
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (addedRow is not null)
                {
                    _context.Entry(addedRow).State = EntityState.Detached;
                }
                else if (existing is not null && originalValues is not null)
                {
                    _context.Entry(existing).CurrentValues.SetValues(originalValues);
                    _context.Entry(existing).State = EntityState.Unchanged;
                }

                throw;
            }
        }
    }

    private static ReadingMemory MapToDomain(ReadingMemoryRow row) =>
        new()
        {
            BookId = row.BookId,
            OpenedBecause = row.OpenedBecause,
            KeyInsight = row.KeyInsight,
            OpenQuestions = row.OpenQuestions,
            Disposition = row.Disposition,
            CreatedAtUtc = row.CreatedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc,
        };
}
