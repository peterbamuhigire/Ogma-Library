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
}

/// <summary>
/// Specifies a bulk mutation to apply across multiple books (FR-CAT-005, V1).
/// </summary>
/// <param name="BookIds">The stable identifiers of the books to edit.</param>
/// <param name="TagsToAdd">Tags to add to every selected book.</param>
/// <param name="TagsToRemove">Tags to remove from every selected book.</param>
/// <param name="ShelfIdToAdd">The shelf to add every selected book to, or <see langword="null"/>.</param>
/// <param name="ShelfIdToRemove">The shelf to remove every selected book from, or <see langword="null"/>.</param>
/// <param name="NewStatus">Override reading status for every selected book, or <see langword="null"/>.</param>
/// <param name="NewRating">Override rating (1–5) for every selected book, or <see langword="null"/>.</param>
public sealed record BulkEditCommand(
    IReadOnlyList<string> BookIds,
    IReadOnlyList<string> TagsToAdd,
    IReadOnlyList<string> TagsToRemove,
    string? ShelfIdToAdd,
    string? ShelfIdToRemove,
    int? NewStatus,
    int? NewRating);
