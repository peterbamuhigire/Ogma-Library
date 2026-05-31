using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Loads and persists per-book reading-memory journal entries
/// (world-class addition, Phase 09).
/// </summary>
public sealed class ReadingMemoryService : IReadingMemoryService
{
    private readonly IReadingMemoryRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="ReadingMemoryService"/>.
    /// </summary>
    /// <param name="repository">The reading-memory persistence repository.</param>
    public ReadingMemoryService(IReadingMemoryRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ReadingMemory> LoadAsync(string bookId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        ReadingMemory? existing = await _repository
            .GetForBookAsync(bookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        // Return an empty entry without persisting — only write on first save.
        return new ReadingMemory
        {
            BookId = bookId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public async Task SaveAsync(ReadingMemory memory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentException.ThrowIfNullOrWhiteSpace(memory.BookId);

        if (memory.Disposition is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memory),
                memory.Disposition,
                "Reading-memory disposition must be between 1 and 5.");
        }

        memory.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(memory, cancellationToken).ConfigureAwait(false);
    }
}
