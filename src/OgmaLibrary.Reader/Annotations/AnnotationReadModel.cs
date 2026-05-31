using System.Reactive.Subjects;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Shared Phase 09 annotation-domain read model used by annotations, bookmarks,
/// and layers. Events are local-only in Phase 09 and feed LAN projection later.
/// </summary>
public sealed class AnnotationReadModel : IAnnotationReadModel, IAnnotationEventPublisher, IDisposable
{
    private readonly Subject<AnnotationEvent> _events = new();

    /// <inheritdoc />
    public IObservable<AnnotationEvent> Events => _events;

    /// <inheritdoc />
    public void Publish(AnnotationEvent annotationEvent)
    {
        ArgumentNullException.ThrowIfNull(annotationEvent);
        _events.OnNext(annotationEvent);
    }

    /// <inheritdoc />
    public void Dispose() => _events.Dispose();
}
