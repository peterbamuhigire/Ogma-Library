namespace OgmaLibrary.Domain;

/// <summary>
/// Temporary read/write access to pre-canonical catalogue rows during Phase 4 migration.
/// It must not be used as an identity authority by new code.
/// </summary>
public interface ILegacyCatalogueRepository
{
    /// <summary>Finds a book by its identity.</summary>
    /// <param name="id">The book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The book, or <see langword="null"/> if none exists.</returns>
    Task<LegacyCatalogueRecord?> FindAsync(BookId id, CancellationToken cancellationToken);

    /// <summary>Adds or updates a book.</summary>
    /// <param name="record">The compatibility record to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(LegacyCatalogueRecord record, CancellationToken cancellationToken);
}

/// <summary>Read access to the canonical identity graph and compatibility aliases.</summary>
public interface ICanonicalIdentityRepository
{
    /// <summary>Resolves a legacy BookId to its canonical path-free projection.</summary>
    /// <param name="legacyBookId">The legacy catalogue identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The canonical projection, or null when no alias exists.</returns>
    Task<CanonicalIdentityProjection?> FindByLegacyBookIdAsync(
        BookId legacyBookId,
        CancellationToken cancellationToken);

    /// <summary>Finds a canonical path-free projection by catalogue item ID.</summary>
    /// <param name="catalogueItemId">The canonical catalogue item identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The canonical projection, or null when it does not exist.</returns>
    Task<CanonicalIdentityProjection?> FindByCatalogueItemIdAsync(
        CatalogueItemId catalogueItemId,
        CancellationToken cancellationToken);

    /// <summary>Resolves the temporary legacy ID used by old search and API clients.</summary>
    /// <param name="catalogueItemId">The canonical catalogue item identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The legacy ID, or null for a post-migration-only item.</returns>
    Task<BookId?> FindLegacyBookIdAsync(
        CatalogueItemId catalogueItemId,
        CancellationToken cancellationToken);
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
/// Read/write access to durable bookmarks (FR-READ-007).
/// </summary>
public interface IBookmarkRepository
{
    /// <summary>Lists bookmarks for a book, ordered by page then creation time.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The bookmarks.</returns>
    Task<IReadOnlyList<Bookmark>> ListForBookAsync(string bookId, CancellationToken cancellationToken);

    /// <summary>Finds a bookmark by its stable identifier.</summary>
    /// <param name="bookmarkId">The stable bookmark identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The bookmark, or <see langword="null"/> if it does not exist.</returns>
    Task<Bookmark?> FindAsync(long bookmarkId, CancellationToken cancellationToken);

    /// <summary>Persists a new bookmark before reporting saved (NFR-OGMA-008).</summary>
    /// <param name="bookmark">The bookmark to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted bookmark with its generated identity.</returns>
    Task<Bookmark> CreateAsync(Bookmark bookmark, CancellationToken cancellationToken);

    /// <summary>Updates the label of an existing bookmark.</summary>
    /// <param name="bookmarkId">The stable bookmark identifier.</param>
    /// <param name="newLabel">The new label text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RenameAsync(long bookmarkId, string newLabel, CancellationToken cancellationToken);

    /// <summary>Deletes a bookmark by its identifier.</summary>
    /// <param name="bookmarkId">The stable bookmark identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteAsync(long bookmarkId, CancellationToken cancellationToken);
}

/// <summary>
/// Read/write access to Phase 09 extended annotations with layer and region support
/// (FR-READ-008, NFR-OGMA-008, ADR-0008).
/// </summary>
public interface IAnnotationV2Repository
{
    /// <summary>Lists all annotations for a book, ordered by page then creation time.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The annotations.</returns>
    Task<IReadOnlyList<AnnotationV2>> ListForBookAsync(string bookId, CancellationToken cancellationToken);

    /// <summary>Lists annotations for a specific page.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The annotations for the page.</returns>
    Task<IReadOnlyList<AnnotationV2>> ListForPageAsync(string bookId, int pageIndex, CancellationToken cancellationToken);

    /// <summary>Finds an annotation by its stable identifier.</summary>
    /// <param name="annotationId">The stable annotation identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The annotation, or <see langword="null"/> if it does not exist.</returns>
    Task<AnnotationV2?> FindAsync(string annotationId, CancellationToken cancellationToken);

    /// <summary>Persists a new annotation inside a transaction before reporting saved (NFR-OGMA-008).</summary>
    /// <param name="annotation">The annotation to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted annotation.</returns>
    Task<AnnotationV2> CreateAsync(AnnotationV2 annotation, CancellationToken cancellationToken);

    /// <summary>Updates the note text and/or color of an existing annotation.</summary>
    /// <param name="annotation">The annotation with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken);

    /// <summary>Deletes an annotation by its identifier.</summary>
    /// <param name="annotationId">The stable annotation identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteAsync(string annotationId, CancellationToken cancellationToken);
}

/// <summary>
/// Read/write access to named annotation layers (world-class addition, Phase 09).
/// </summary>
public interface IAnnotationLayerRepository
{
    /// <summary>Lists all layers for a book, ordered by sort order.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The layers.</returns>
    Task<IReadOnlyList<AnnotationLayer>> ListForBookAsync(string bookId, CancellationToken cancellationToken);

    /// <summary>Finds an annotation layer by its stable identifier.</summary>
    /// <param name="layerId">The stable layer identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The layer, or <see langword="null"/> if it does not exist.</returns>
    Task<AnnotationLayer?> FindAsync(string layerId, CancellationToken cancellationToken);

    /// <summary>Persists a new layer.</summary>
    /// <param name="layer">The layer to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted layer.</returns>
    Task<AnnotationLayer> CreateAsync(AnnotationLayer layer, CancellationToken cancellationToken);

    /// <summary>Updates layer name, color, or visibility.</summary>
    /// <param name="layer">The layer with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateAsync(AnnotationLayer layer, CancellationToken cancellationToken);

    /// <summary>Deletes a layer by its identifier. The caller must reassign any orphaned annotations first.</summary>
    /// <param name="layerId">The stable layer identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteAsync(string layerId, CancellationToken cancellationToken);

    /// <summary>Moves all annotations from the source layer to the target layer.</summary>
    /// <param name="sourceLayerId">The layer to empty.</param>
    /// <param name="targetLayerId">The layer to receive the annotations.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task MergeIntoAsync(string sourceLayerId, string targetLayerId, CancellationToken cancellationToken);
}

/// <summary>
/// Read/write access to reading-memory journal entries (world-class addition, Phase 09).
/// </summary>
public interface IReadingMemoryRepository
{
    /// <summary>Gets the reading-memory entry for a book, or <see langword="null"/> if none exists.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The reading memory, or <see langword="null"/>.</returns>
    Task<ReadingMemory?> GetForBookAsync(string bookId, CancellationToken cancellationToken);

    /// <summary>Upserts a reading-memory entry (creates if absent, updates if present).</summary>
    /// <param name="memory">The reading memory to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(ReadingMemory memory, CancellationToken cancellationToken);
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
