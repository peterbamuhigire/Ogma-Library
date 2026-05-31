using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Manages named annotation layers for a book (world-class addition, Phase 09).
/// </summary>
/// <remarks>
/// <para>
/// The at-least-one constraint is enforced by this service: <see cref="DeleteAsync"/>
/// throws <see cref="InvalidOperationException"/> when the book has only one layer.
/// </para>
/// </remarks>
public interface IAnnotationLayerService
{
    /// <summary>Returns all layers for a book, ordered by sort order.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The layers.</returns>
    Task<IReadOnlyList<AnnotationLayer>> GetLayersAsync(
        string bookId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new layer for a book.  Assigns the next sort order automatically.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="name">The layer display name.</param>
    /// <param name="color">The layer highlight colour as a hex string.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created layer.</returns>
    Task<AnnotationLayer> CreateLayerAsync(
        string bookId,
        string name,
        string color,
        CancellationToken cancellationToken);

    /// <summary>Renames an existing layer.</summary>
    /// <param name="layerId">The stable layer identifier.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RenameLayerAsync(string layerId, string newName, CancellationToken cancellationToken);

    /// <summary>Toggles the visibility of a layer.</summary>
    /// <param name="layerId">The stable layer identifier.</param>
    /// <param name="isVisible">The desired visibility state.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetVisibilityAsync(string layerId, bool isVisible, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a layer.  All annotations in the layer are set to the implicit default
    /// (null LayerId) via the cascading SetNull FK.
    /// </summary>
    /// <param name="bookId">The stable book identity (needed for the at-least-one check).</param>
    /// <param name="layerId">The stable layer identifier to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is the last remaining layer for the book.
    /// </exception>
    Task DeleteAsync(string bookId, string layerId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves all annotations from the source layer into the target layer,
    /// then deletes the source layer.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="sourceLayerId">The layer to empty and delete.</param>
    /// <param name="targetLayerId">The layer to receive the annotations.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task MergeLayersAsync(
        string bookId,
        string sourceLayerId,
        string targetLayerId,
        CancellationToken cancellationToken);
}
