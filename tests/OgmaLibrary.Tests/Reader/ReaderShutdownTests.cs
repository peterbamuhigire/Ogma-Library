using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Reader.Progress;

namespace OgmaLibrary.Tests.Reader;

/// <summary>Closing the app must not lose a page turn made inside the debounce window (READ-013).</summary>
public sealed class ReaderShutdownTests
{
    [Fact]
    public async Task Shutdown_FlushesPendingProgress()
    {
        var repository = new RecordingRepository();
        using var progress = new ReadingProgressService(repository);

        // Scheduled saves wait 500 ms; shutdown happens straight away.
        progress.ScheduleSave("book-1", new ReaderProgress(7, 0.5, ZoomMode.FitWidth, 100, DisplayMode.SinglePage));
        Assert.Empty(repository.Saved);

        bool completed = await ReaderShutdown.FlushAsync(null, progress, TimeSpan.FromSeconds(5));

        Assert.True(completed);
        Assert.Equal(("book-1", 7), Assert.Single(repository.Saved));

        // Nothing is written twice when the debounce timer would have fired later.
        await Task.Delay(700);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Shutdown_StopsWaitingWhenTheBudgetIsSpent()
    {
        var repository = new RecordingRepository { Delay = TimeSpan.FromSeconds(30) };
        using var progress = new ReadingProgressService(repository);
        progress.ScheduleSave("book-1", ReaderProgress.Default);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool completed = await ReaderShutdown.FlushAsync(null, progress, TimeSpan.FromMilliseconds(200));

        Assert.False(completed);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private sealed class RecordingRepository : IReadingProgressRepository
    {
        private readonly List<(string BookId, int Page)> _saved = [];

        public TimeSpan Delay { get; init; }

        public IReadOnlyList<(string BookId, int Page)> Saved
        {
            get
            {
                lock (_saved)
                {
                    return [.. _saved];
                }
            }
        }

        public Task<ReadingProgress?> GetAsync(string relativePath, CancellationToken cancellationToken) =>
            Task.FromResult<ReadingProgress?>(null);

        public Task<ReadingProgress?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken) =>
            Task.FromResult<ReadingProgress?>(null);

        public Task SaveAsync(string relativePath, ReadingProgress progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async Task SaveByBookIdAsync(string bookId, ReadingProgress progress, CancellationToken cancellationToken)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            }

            lock (_saved)
            {
                _saved.Add((bookId, progress.LastPageIndex));
            }
        }
    }
}
