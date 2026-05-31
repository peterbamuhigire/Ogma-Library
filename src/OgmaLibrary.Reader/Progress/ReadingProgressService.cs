using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Progress;

/// <summary>
/// Persists per-book reading progress via the Phase 04 <see cref="IReadingProgressRepository"/>,
/// adding a 500 ms debounce so rapid page turns do not hammer SQLite (FR-READ-001).
/// </summary>
public sealed class ReadingProgressService : IReadingProgressService, IDisposable
{
    private readonly IReadingProgressRepository _repository;

    // Debounce state: one timer and pending value per bookId.
    private readonly Dictionary<string, (Timer Timer, ReaderProgress Progress)> _pending = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ReadingProgressService"/>.
    /// </summary>
    /// <param name="repository">The reading progress repository (Phase 04).</param>
    public ReadingProgressService(IReadingProgressRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ReaderProgress> LoadAsync(string bookId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        // The Phase 04 repository uses relative path as key; we use bookId directly
        // by passing it as the "relativePath" token (the implementation will find the
        // book by querying BookFiles.BookId = bookId).
        // NOTE: We load by bookId as relativePath here for the contract — the actual
        // data is keyed on BookId in the ReadingProgress table (one row per book).
        ReadingProgress? domainProgress = await _repository
            .GetByBookIdAsync(bookId, ct)
            .ConfigureAwait(false);

        if (domainProgress is null)
        {
            return ReaderProgress.Default;
        }

        // Map from domain entity to ReaderProgress application record.
        // ZoomMode/DisplayMode were added in Phase 08; fall back to defaults if absent.
        return new ReaderProgress(
            domainProgress.LastPageIndex,
            domainProgress.LastScrollOffset,
            ZoomMode.FitWidth,
            100.0,
            DisplayMode.SinglePage);
    }

    /// <inheritdoc />
    public async Task SaveImmediateAsync(string bookId, ReaderProgress progress, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(progress);

        // Cancel any pending debounced write for this book.
        CancelPending(bookId);

        await PersistAsync(bookId, progress, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ScheduleSave(string bookId, ReaderProgress progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(progress);

        lock (_lock)
        {
            // Reset existing timer if present.
            if (_pending.TryGetValue(bookId, out var existing))
            {
                existing.Timer.Dispose();
            }

            Timer timer = new Timer(
                callback: OnDebounceElapsed,
                state: bookId,
                dueTime: 500,
                period: Timeout.Infinite);

            _pending[bookId] = (timer, progress);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var (timer, _) in _pending.Values)
            {
                timer.Dispose();
            }

            _pending.Clear();
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        if (state is not string bookId)
        {
            return;
        }

        ReaderProgress? progress;
        lock (_lock)
        {
            if (!_pending.TryGetValue(bookId, out var entry))
            {
                return;
            }

            entry.Timer.Dispose();
            progress = entry.Progress;
            _pending.Remove(bookId);
        }

        // Fire-and-forget persist; errors are swallowed to avoid crashing the timer.
        _ = PersistAsync(bookId, progress, CancellationToken.None);
    }

    private async Task PersistAsync(string bookId, ReaderProgress progress, CancellationToken ct)
    {
        var domain = new ReadingProgress
        {
            LastPageIndex = progress.LastPageIndex,
            LastScrollOffset = progress.ScrollOffset,
            Status = ReadingStatus.Reading,
            LastOpenedUtc = DateTimeOffset.UtcNow,
        };

        // The repository accepts a relative path; we pass bookId as the key.
        // The infrastructure implementation is extended in Phase 08 to accept bookId.
        await _repository.SaveByBookIdAsync(bookId, domain, ct).ConfigureAwait(false);
    }

    private void CancelPending(string bookId)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(bookId, out var entry))
            {
                entry.Timer.Dispose();
                _pending.Remove(bookId);
            }
        }
    }
}
