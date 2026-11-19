namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Write-side service for the Library Catalogue bounded context (FR-CAT-003,
/// FR-CAT-004, FR-CAT-005). All mutations to shelves, metadata fields, and
/// bulk operations flow through this interface so the Bookshelf Presentation
/// context never writes to the catalogue directly.
/// </summary>
public interface ICatalogueWriteService
{
    // ── Shelf CRUD (FR-CAT-003) ───────────────────────────────────────────────

    /// <summary>Creates a new virtual or smart shelf.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="isSmart">True for a smart (query-defined) shelf.</param>
    /// <param name="query">The JSON query for smart shelves; <see langword="null"/> for virtual shelves.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stable identifier of the created shelf.</returns>
    Task<string> CreateShelfAsync(
        string name,
        bool isSmart = false,
        string? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>Renames a shelf.</summary>
    /// <param name="shelfId">The shelf to rename.</param>
    /// <param name="newName">The new display name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a shelf and removes all its book assignments.</summary>
    /// <param name="shelfId">The shelf to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default);

    // ── Shelf membership (FR-CAT-003) ─────────────────────────────────────────

    /// <summary>Adds a book to a shelf.</summary>
    /// <param name="shelfId">The shelf.</param>
    /// <param name="bookId">The book to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task AddBookToShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default);

    /// <summary>Removes a book from a shelf.</summary>
    /// <param name="shelfId">The shelf.</param>
    /// <param name="bookId">The book to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveBookFromShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default);

    // ── Metadata field edit (FR-CAT-004) ─────────────────────────────────────

    /// <summary>
    /// Updates a single metadata field on a book and records an audit event.
    /// </summary>
    /// <param name="bookId">The book to update.</param>
    /// <param name="fieldName">The field name (e.g. "Title").</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateMetadataFieldAsync(
        string bookId,
        string fieldName,
        string? value,
        CancellationToken cancellationToken = default);

    // ── Bulk edit (FR-CAT-005, V1) ────────────────────────────────────────────

    /// <summary>
    /// Applies a bulk edit to multiple books and records a before/after audit
    /// snapshot to <c>AuditEvents</c>. Used by <see cref="Commands.ICommandHistory"/>
    /// for undo support.
    /// </summary>
    /// <param name="command">The bulk edit specification.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the fields a bulk edit can change, for every listed book, so the edit can be
    /// undone (Sept-23 Phase 10, task 10.8). Unknown book identifiers are skipped.
    /// </summary>
    /// <param name="bookIds">The books to capture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>One snapshot per existing book.</returns>
    Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores books to captured snapshots in one transaction: authors, year, lifecycle status,
    /// rating, user tags and manual shelf membership. Shelves deleted since the capture are skipped.
    /// </summary>
    /// <param name="snapshots">The snapshots taken by <see cref="CaptureBulkEditSnapshotAsync"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RestoreBulkEditSnapshotAsync(
        IReadOnlyList<BulkEditSnapshot> snapshots,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Specifies a bulk mutation to apply across multiple books (FR-CAT-005, V1).
/// </summary>
/// <param name="BookIds">The stable identifiers of the books to edit.</param>
/// <param name="TagsToAdd">Tags to add to every selected book.</param>
/// <param name="TagsToRemove">Tags to remove from every selected book.</param>
/// <param name="ShelfIdToAdd">The shelf to add every selected book to, or <see langword="null"/>.</param>
/// <param name="ShelfIdToRemove">The shelf to remove every selected book from, or <see langword="null"/>.</param>
/// <param name="NewLifecycleStatus">
/// Override the catalogue lifecycle status (Active, Unavailable, Excluded) for every selected book,
/// or <see langword="null"/>. This is not the personal reading status, which goes through
/// <see cref="IBookCurationService"/> (Sept-23 Phase 10, K116).
/// </param>
/// <param name="NewRating">Override rating (1–5) for every selected book, or <see langword="null"/>.</param>
/// <param name="AuthorToSet">
/// Replace every selected book's authors, or <see langword="null"/>. Several authors are separated
/// by semicolons and validated by <see cref="BulkEditFieldRules.TryParseAuthors"/>.
/// </param>
/// <param name="YearToSet">
/// Set every selected book's year, or <see langword="null"/>; validated by
/// <see cref="BulkEditFieldRules.IsValidYear"/>.
/// </param>
public sealed record BulkEditCommand(
    IReadOnlyList<string> BookIds,
    IReadOnlyList<string> TagsToAdd,
    IReadOnlyList<string> TagsToRemove,
    string? ShelfIdToAdd,
    string? ShelfIdToRemove,
    int? NewLifecycleStatus,
    int? NewRating,
    string? AuthorToSet = null,
    int? YearToSet = null);

/// <summary>
/// The before-state of one book for undoing a bulk edit (Sept-23 Phase 10, task 10.8).
/// </summary>
/// <param name="BookId">The book.</param>
/// <param name="Authors">Linked author names in display order.</param>
/// <param name="UserAuthorField">The user-entered Author metadata value, or <see langword="null"/> when there was none.</param>
/// <param name="HasUserAuthorField">Whether a user-entered Author metadata row existed.</param>
/// <param name="Year">The year.</param>
/// <param name="LifecycleStatus">The catalogue lifecycle status.</param>
/// <param name="Rating">The rating.</param>
/// <param name="UserTags">The user Tags metadata value, or <see langword="null"/>.</param>
/// <param name="HasUserTags">Whether a user Tags metadata row existed.</param>
/// <param name="ManualShelfIds">The manual shelves the book belonged to.</param>
public sealed record BulkEditSnapshot(
    string BookId,
    IReadOnlyList<string> Authors,
    string? UserAuthorField,
    bool HasUserAuthorField,
    int? Year,
    int LifecycleStatus,
    int? Rating,
    string? UserTags,
    bool HasUserTags,
    IReadOnlyList<string> ManualShelfIds);
