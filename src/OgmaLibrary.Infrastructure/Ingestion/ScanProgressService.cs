using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Thread-safe scan progress aggregator. Background worker threads call the mutating
/// methods; the <see cref="ProgressChanged"/> event is raised synchronously on the
/// calling thread so Avalonia UI code can marshal via <c>Dispatcher.UIThread.Post</c>
/// (NFR-PROD-005).
/// </summary>
public sealed class ScanProgressService : IScanProgressService
{
    private volatile ScanProgressSnapshot _snapshot =
        new(ScanPhase.Idle, 0, 0, 0, IsCancellable: false);

    private int _filesDiscovered;
    private int _filesCompleted;
    private int _filesFailed;

    /// <inheritdoc />
    public ScanProgressSnapshot CurrentSnapshot => _snapshot;

    /// <inheritdoc />
    public event EventHandler<ScanProgressSnapshot>? ProgressChanged;

    /// <inheritdoc />
    public void SetPhase(ScanPhase phase)
    {
        bool cancellable = phase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;
        Publish(new ScanProgressSnapshot(
            phase,
            _filesDiscovered,
            _filesCompleted,
            _filesFailed,
            IsCancellable: cancellable));
    }

    /// <inheritdoc />
    public void IncrementDiscovered()
    {
        int discovered = Interlocked.Increment(ref _filesDiscovered);
        PublishCounts(discovered, _filesCompleted, _filesFailed);
    }

    /// <inheritdoc />
    public void IncrementCompleted()
    {
        int completed = Interlocked.Increment(ref _filesCompleted);
        PublishCounts(_filesDiscovered, completed, _filesFailed);
    }

    /// <inheritdoc />
    public void IncrementFailed()
    {
        int failed = Interlocked.Increment(ref _filesFailed);
        PublishCounts(_filesDiscovered, _filesCompleted, failed);
    }

    /// <inheritdoc />
    public void Reset()
    {
        Interlocked.Exchange(ref _filesDiscovered, 0);
        Interlocked.Exchange(ref _filesCompleted, 0);
        Interlocked.Exchange(ref _filesFailed, 0);
        Publish(new ScanProgressSnapshot(ScanPhase.Idle, 0, 0, 0, IsCancellable: false));
    }

    private void PublishCounts(int discovered, int completed, int failed)
    {
        var phase = _snapshot.Phase;
        bool cancellable = phase is ScanPhase.Discovering or ScanPhase.Processing or ScanPhase.GeneratingAssets;
        Publish(new ScanProgressSnapshot(phase, discovered, completed, failed, cancellable));
    }

    private void Publish(ScanProgressSnapshot snapshot)
    {
        _snapshot = snapshot;
        ProgressChanged?.Invoke(this, snapshot);
    }
}
