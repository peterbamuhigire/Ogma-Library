using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Emits a stream of annotation events consumable by the local UI and — in Phase 17 —
/// by the LAN host projection layer for per-student state sync.
/// </summary>
/// <remarks>
/// <para>
/// LAN-projection-ready design: this interface is defined in Phase 09 so Phase 17 can
/// wire it to the LAN host without retrofitting the architecture.  In Phase 09 only
/// the annotation overlay panel subscribes locally.
/// </para>
/// </remarks>
public interface IAnnotationReadModel
{
    /// <summary>
    /// An observable sequence of annotation events raised as highlights, notes,
    /// bookmarks, and layers change.
    /// </summary>
    IObservable<AnnotationEvent> Events { get; }
}

/// <summary>
/// Publishes annotation-domain events into the shared Phase 09 read model.
/// </summary>
public interface IAnnotationEventPublisher
{
    /// <summary>Publishes a successfully persisted annotation-domain event.</summary>
    /// <param name="annotationEvent">The event to publish.</param>
    void Publish(AnnotationEvent annotationEvent);
}

/// <summary>
/// Discriminated union of annotation lifecycle events (LAN-CLASSROOM-ARCHITECTURE §3).
/// </summary>
public abstract record AnnotationEvent
{
    private AnnotationEvent() { }

    /// <summary>A new annotation was created and persisted.</summary>
    /// <param name="BookId">The book the annotation belongs to.</param>
    /// <param name="Annotation">The created annotation.</param>
    public sealed record AnnotationCreated(string BookId, AnnotationV2 Annotation) : AnnotationEvent;

    /// <summary>An annotation was updated.</summary>
    /// <param name="BookId">The book the annotation belongs to.</param>
    /// <param name="Annotation">The updated annotation.</param>
    public sealed record AnnotationUpdated(string BookId, AnnotationV2 Annotation) : AnnotationEvent;

    /// <summary>An annotation was deleted.</summary>
    /// <param name="BookId">The book the annotation belonged to.</param>
    /// <param name="AnnotationId">The deleted annotation's identifier.</param>
    public sealed record AnnotationDeleted(string BookId, string AnnotationId) : AnnotationEvent;

    /// <summary>A bookmark was created.</summary>
    /// <param name="BookId">The book the bookmark belongs to.</param>
    /// <param name="Bookmark">The created bookmark.</param>
    public sealed record BookmarkCreated(string BookId, Bookmark Bookmark) : AnnotationEvent;

    /// <summary>A bookmark was deleted.</summary>
    /// <param name="BookId">The book the bookmark belonged to.</param>
    /// <param name="BookmarkId">The deleted bookmark's identifier.</param>
    public sealed record BookmarkDeleted(string BookId, long BookmarkId) : AnnotationEvent;

    /// <summary>A bookmark label or position was updated.</summary>
    /// <param name="BookId">The book the bookmark belongs to.</param>
    /// <param name="Bookmark">The updated bookmark.</param>
    public sealed record BookmarkUpdated(string BookId, Bookmark Bookmark) : AnnotationEvent;

    /// <summary>A layer was created, renamed, had its visibility changed, or was deleted.</summary>
    /// <param name="BookId">The book the layer belongs to.</param>
    /// <param name="LayerId">The affected layer identifier.</param>
    public sealed record LayerChanged(string BookId, string LayerId) : AnnotationEvent;
}
