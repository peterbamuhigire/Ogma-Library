using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Creates, updates, and queries highlights and notes for the current reader session
/// (FR-READ-008, NFR-OGMA-008, ADR-0008).
/// </summary>
/// <remarks>
/// <para>
/// All write operations persist to the catalogue of record inside a transaction before
/// returning so the UI can show a "Saved" indicator only after the <see langword="await"/>
/// completes.  This satisfies NFR-OGMA-008 (annotation durable before reported saved).
/// </para>
/// <para>
/// Coordinates are stored as normalized <see cref="AnnotationRegion"/> values (see
/// <see cref="AnnotationRegion"/> for the rotation-safe coordinate model).
/// </para>
/// </remarks>
public interface IAnnotationService
{
    /// <summary>
    /// Creates a highlight annotation for the given selection regions.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="layerId">The target layer identifier, or <see langword="null"/> for the implicit default layer.</param>
    /// <param name="regions">Normalized bounding regions covering the selection.</param>
    /// <param name="color">Highlight colour as a hex string (e.g. <c>"#FFD700"</c>).</param>
    /// <param name="quoteText">The highlighted text passage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted annotation.</returns>
    Task<AnnotationV2> CreateHighlightAsync(
        string bookId,
        string? layerId,
        IReadOnlyList<AnnotationRegion> regions,
        string color,
        string? quoteText,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a note annotation anchored to a region.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="layerId">The target layer identifier, or <see langword="null"/> for the implicit default layer.</param>
    /// <param name="region">The anchor region for the note icon.</param>
    /// <param name="noteText">The note body text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted annotation.</returns>
    Task<AnnotationV2> CreateNoteAsync(
        string bookId,
        string? layerId,
        AnnotationRegion region,
        string noteText,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the note text and/or color of an existing annotation.
    /// </summary>
    /// <param name="annotation">The annotation with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an annotation by its identifier.
    /// </summary>
    /// <param name="annotationId">The stable annotation identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteAsync(string annotationId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all annotations for a specific page.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The annotations for the page.</returns>
    Task<IReadOnlyList<AnnotationV2>> GetForPageAsync(
        string bookId,
        int pageIndex,
        CancellationToken cancellationToken);
}
