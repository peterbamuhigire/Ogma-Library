using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Loads and saves per-book reading-memory journal entries
/// (world-class addition, Phase 09).
/// </summary>
/// <remarks>
/// The reading-memory journal is auto-saved when a field loses focus with a 1-second
/// debounce; this service handles the actual persistence.  Phase 13 reads this journal
/// as first-party evidence for AI advisor recommendations.
/// </remarks>
public interface IReadingMemoryService
{
    /// <summary>
    /// Loads the reading-memory entry for a book, creating a new empty entry if none
    /// exists yet.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The reading-memory entry (never <see langword="null"/>).</returns>
    Task<ReadingMemory> LoadAsync(string bookId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a reading-memory entry.  Call after the user blurs a field (debounced
    /// by the caller to ≥ 1 second).
    /// </summary>
    /// <param name="memory">The entry to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(ReadingMemory memory, CancellationToken cancellationToken);
}
