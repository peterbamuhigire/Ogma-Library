namespace OgmaLibrary.Infrastructure.ClassroomClient;

internal sealed class ObservableEvents<TEvent> : IObservable<TEvent>
{
    private readonly object _gate = new();
    private readonly List<IObserver<TEvent>> _observers = [];

    public IDisposable Subscribe(IObserver<TEvent> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    public void Publish(TEvent value)
    {
        IObserver<TEvent>[] observers;
        lock (_gate)
        {
            observers = _observers.ToArray();
        }

        foreach (IObserver<TEvent> observer in observers)
        {
            observer.OnNext(value);
        }
    }

    private void Unsubscribe(IObserver<TEvent> observer)
    {
        lock (_gate)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ObservableEvents<TEvent> _owner;
        private IObserver<TEvent>? _observer;

        public Subscription(ObservableEvents<TEvent> owner, IObserver<TEvent> observer)
        {
            _owner = owner;
            _observer = observer;
        }

        public void Dispose()
        {
            IObserver<TEvent>? observer = Interlocked.Exchange(ref _observer, null);
            if (observer is not null)
            {
                _owner.Unsubscribe(observer);
            }
        }
    }
}
