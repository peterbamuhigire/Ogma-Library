using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Creates, updates, queries, and deletes highlights and notes for the active reader
/// session (FR-READ-008, NFR-OGMA-008, ADR-0008).
/// Emits <see cref="AnnotationEvent"/> values on the <see cref="IAnnotationReadModel"/>
/// event stream after each successful write.
/// </summary>
public sealed class AnnotationService : IAnnotationService, IAnnotationReadModel, IDisposable
{
    private readonly IAnnotationV2Repository _repository;
    private readonly IAnnotationEventPublisher _events;
    private readonly IAnnotationReadModel _readModel;
    private readonly AnnotationReadModel? _ownedReadModel;

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationService"/>.
    /// </summary>
    /// <param name="repository">The annotation persistence repository.</param>
    /// <param name="events">The shared annotation event publisher.</param>
    public AnnotationService(IAnnotationV2Repository repository, IAnnotationEventPublisher? events = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        if (events is null)
        {
            var readModel = new AnnotationReadModel();
            _events = readModel;
            _readModel = readModel;
            _ownedReadModel = readModel;
        }
        else
        {
            _events = events;
            _readModel = events as IAnnotationReadModel
                ?? throw new ArgumentException(
                    "Annotation event publisher must also expose the read model stream.",
                    nameof(events));
        }
    }

    /// <inheritdoc />
    public IObservable<AnnotationEvent> Events => _readModel.Events;

    /// <inheritdoc />
    public async Task<AnnotationV2> CreateHighlightAsync(
        string bookId,
        string? layerId,
        IReadOnlyList<AnnotationRegion> regions,
        string color,
        string? quoteText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        var now = DateTimeOffset.UtcNow;
        var annotation = new AnnotationV2
        {
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant()[..26],
            BookId = bookId,
            LayerId = layerId,
            Kind = AnnotationKind.Highlight,
            Regions = regions,
            HighlightColor = color,
            QuoteText = quoteText,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        AnnotationV2 saved = await _repository
            .CreateAsync(annotation, cancellationToken)
            .ConfigureAwait(false);

        _events.Publish(new AnnotationEvent.AnnotationCreated(bookId, saved));
        return saved;
    }

    /// <inheritdoc />
    public async Task<AnnotationV2> CreateNoteAsync(
        string bookId,
        string? layerId,
        AnnotationRegion region,
        string noteText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteText);

        var now = DateTimeOffset.UtcNow;
        var annotation = new AnnotationV2
        {
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant()[..26],
            BookId = bookId,
            LayerId = layerId,
            Kind = AnnotationKind.Note,
            Regions = [region],
            NoteText = noteText,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        AnnotationV2 saved = await _repository
            .CreateAsync(annotation, cancellationToken)
            .ConfigureAwait(false);

        _events.Publish(new AnnotationEvent.AnnotationCreated(bookId, saved));
        return saved;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        annotation.ModifiedUtc = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(annotation, cancellationToken).ConfigureAwait(false);
        _events.Publish(new AnnotationEvent.AnnotationUpdated(annotation.BookId, annotation));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string annotationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);

        AnnotationV2? existing = await _repository
            .FindAsync(annotationId, cancellationToken)
            .ConfigureAwait(false);

        await _repository.DeleteAsync(annotationId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            _events.Publish(new AnnotationEvent.AnnotationDeleted(existing.BookId, annotationId));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AnnotationV2>> GetForPageAsync(
        string bookId,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        return _repository.ListForPageAsync(bookId, pageIndex, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _ownedReadModel?.Dispose();
}
