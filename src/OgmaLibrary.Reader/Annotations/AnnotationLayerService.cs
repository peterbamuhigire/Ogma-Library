using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Manages named annotation layers for a book (world-class addition, Phase 09).
/// Enforces the at-least-one-layer constraint.
/// </summary>
public sealed class AnnotationLayerService : IAnnotationLayerService
{
    private readonly IAnnotationLayerRepository _repository;
    private readonly IAnnotationEventPublisher? _events;

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationLayerService"/>.
    /// </summary>
    /// <param name="repository">The layer persistence repository.</param>
    /// <param name="events">The shared annotation event publisher.</param>
    public AnnotationLayerService(
        IAnnotationLayerRepository repository,
        IAnnotationEventPublisher? events = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _events = events;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AnnotationLayer>> GetLayersAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        return _repository.ListForBookAsync(bookId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AnnotationLayer> CreateLayerAsync(
        string bookId,
        string name,
        string color,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        IReadOnlyList<AnnotationLayer> existing = await _repository
            .ListForBookAsync(bookId, cancellationToken)
            .ConfigureAwait(false);

        var layer = new AnnotationLayer
        {
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant()[..26],
            BookId = bookId,
            Name = name,
            Color = color,
            IsVisible = true,
            SortOrder = existing.Count,
        };

        AnnotationLayer saved = await _repository.CreateAsync(layer, cancellationToken)
            .ConfigureAwait(false);
        _events?.Publish(new AnnotationEvent.LayerChanged(bookId, saved.Id));
        return saved;
    }

    /// <inheritdoc />
    public async Task RenameLayerAsync(
        string layerId,
        string newName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        AnnotationLayer? layer = await _repository
            .FindAsync(layerId, cancellationToken)
            .ConfigureAwait(false);
        if (layer is null)
        {
            return;
        }

        layer.Name = newName;
        await _repository.UpdateAsync(layer, cancellationToken).ConfigureAwait(false);
        _events?.Publish(new AnnotationEvent.LayerChanged(layer.BookId, layer.Id));
    }

    /// <inheritdoc />
    public async Task SetVisibilityAsync(
        string layerId,
        bool isVisible,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        AnnotationLayer? layer = await _repository
            .FindAsync(layerId, cancellationToken)
            .ConfigureAwait(false);
        if (layer is null)
        {
            return;
        }

        layer.IsVisible = isVisible;
        await _repository.UpdateAsync(layer, cancellationToken).ConfigureAwait(false);
        _events?.Publish(new AnnotationEvent.LayerChanged(layer.BookId, layer.Id));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string bookId,
        string layerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        IReadOnlyList<AnnotationLayer> layers = await _repository
            .ListForBookAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        AnnotationLayer? layer = layers.FirstOrDefault(l => l.Id == layerId);
        if (layer is null)
        {
            return;
        }

        if (layers.Count <= 1)
        {
            throw new InvalidOperationException(
                "Cannot delete the last remaining annotation layer. " +
                "At least one layer must always be present.");
        }

        AnnotationLayer targetLayer = layers
            .Where(l => l.Id != layerId)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Id, StringComparer.Ordinal)
            .First();

        await _repository
            .MergeIntoAsync(layerId, targetLayer.Id, cancellationToken)
            .ConfigureAwait(false);
        await _repository.DeleteAsync(layerId, cancellationToken).ConfigureAwait(false);
        _events?.Publish(new AnnotationEvent.LayerChanged(bookId, layerId));
        _events?.Publish(new AnnotationEvent.LayerChanged(bookId, targetLayer.Id));
    }

    /// <inheritdoc />
    public async Task MergeLayersAsync(
        string bookId,
        string sourceLayerId,
        string targetLayerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLayerId);

        if (sourceLayerId == targetLayerId)
        {
            throw new InvalidOperationException("Cannot merge an annotation layer into itself.");
        }

        IReadOnlyList<AnnotationLayer> layers = await _repository
            .ListForBookAsync(bookId, cancellationToken)
            .ConfigureAwait(false);

        if (!layers.Any(l => l.Id == sourceLayerId) || !layers.Any(l => l.Id == targetLayerId))
        {
            throw new InvalidOperationException("Both annotation layers must belong to the requested book.");
        }

        await _repository.MergeIntoAsync(sourceLayerId, targetLayerId, cancellationToken)
            .ConfigureAwait(false);
        await DeleteAsync(bookId, sourceLayerId, cancellationToken).ConfigureAwait(false);
        _events?.Publish(new AnnotationEvent.LayerChanged(bookId, targetLayerId));
    }
}
