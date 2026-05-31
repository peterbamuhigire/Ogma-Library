namespace OgmaLibrary.Domain;

/// <summary>
/// Read/write access to <see cref="Book"/> records in the catalogue of record.
/// Implemented by the Infrastructure layer; the Domain owns only the contract so
/// book identity is never coupled to a storage technology (HLD §2.2).
/// </summary>
public interface IBookRepository
{
    /// <summary>Finds a book by its identity.</summary>
    /// <param name="id">The book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The book, or <see langword="null"/> if none exists.</returns>
    Task<Book?> FindAsync(BookId id, CancellationToken cancellationToken);

    /// <summary>Adds or updates a book.</summary>
    /// <param name="book">The book to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(Book book, CancellationToken cancellationToken);
}

/// <summary>Read/write access to virtual and smart shelves (FR-CAT-003).</summary>
public interface IShelfRepository
{
    /// <summary>Lists all shelves.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The shelves.</returns>
    Task<IReadOnlyList<Shelf>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Adds or updates a shelf.</summary>
    /// <param name="shelf">The shelf to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(Shelf shelf, CancellationToken cancellationToken);
}

/// <summary>Read/write access to highlights and notes (FR-READ-008).</summary>
public interface IAnnotationRepository
{
    /// <summary>Lists the annotations for a file, in page order.</summary>
    /// <param name="relativePath">The file's path relative to the library root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The annotations.</returns>
    Task<IReadOnlyList<Annotation>> ListForFileAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Persists an annotation before it is reported saved (NFR-OGMA-008).</summary>
    /// <param name="relativePath">The file the annotation anchors to.</param>
    /// <param name="annotation">The annotation to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(string relativePath, Annotation annotation, CancellationToken cancellationToken);
}

/// <summary>Read/write access to per-file reading progress (FR-READ-001).</summary>
public interface IReadingProgressRepository
{
    /// <summary>Gets the saved progress for a file, if any.</summary>
    /// <param name="relativePath">The file's path relative to the library root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The progress, or <see langword="null"/> if the file was never opened.</returns>
    Task<ReadingProgress?> GetAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Gets the saved progress for a book by its stable identity (Phase 08).</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The progress, or <see langword="null"/> if the book was never opened.</returns>
    Task<ReadingProgress?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken);

    /// <summary>Persists the reading progress for a file.</summary>
    /// <param name="relativePath">The file the progress belongs to.</param>
    /// <param name="progress">The progress to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(string relativePath, ReadingProgress progress, CancellationToken cancellationToken);

    /// <summary>Persists reading progress keyed by the book's stable identity (Phase 08).</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="progress">The progress to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveByBookIdAsync(string bookId, ReadingProgress progress, CancellationToken cancellationToken);
}

/// <summary>
/// Append-only access to the local audit trail (NFR-PROD-013). The contract exposes
/// no delete or update operation by design — audit entries are immutable.
/// </summary>
public interface IAuditRepository
{
    /// <summary>Appends an audit event.</summary>
    /// <param name="auditEvent">The event to append.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    /// <summary>Reads recent audit events, most recent first.</summary>
    /// <param name="maxCount">The maximum number of events to return.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The audit events.</returns>
    Task<IReadOnlyList<AuditEvent>> ReadRecentAsync(int maxCount, CancellationToken cancellationToken);
}
