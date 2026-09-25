using System.ComponentModel;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 02 (T02.5, K72): concurrent catalogue refreshes must be single-flight and must
/// never leave duplicate or torn entries.
/// </summary>
public sealed class CatalogueRefreshConcurrencyTests
{
    private const int BookCount = 60;

    [AvaloniaFact]
    public async Task LoadAsync_TwentyConcurrentRefreshes_LeaveEachBookExactlyOnce()
    {
        var viewModel = new CatalogueViewModel(
            new SlowReadModel(CreateBooks(BookCount)),
            new NoOpNavigation(),
            new InMemoryLocalizationService());

        Task[] refreshes = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => viewModel.LoadAsync()))
            .ToArray();
        await Task.WhenAll(refreshes);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(BookCount, viewModel.TotalCount);
        Assert.Equal(BookCount, viewModel.TotalFilteredCount);
        Assert.Equal(
            viewModel.FilteredItems.Count,
            viewModel.FilteredItems.Select(book => book.BookId).Distinct(StringComparer.Ordinal).Count());
        Assert.False(viewModel.IsLoading);
        viewModel.Dispose();
    }

    [AvaloniaFact]
    public async Task LoadAsync_WithUiDispatcher_RaisesBoundChangesInsideTheDispatcher()
    {
        var dispatcher = new RecordingDispatcher();
        var viewModel = new CatalogueViewModel(
            new SlowReadModel(CreateBooks(10)),
            new NoOpNavigation(),
            new InMemoryLocalizationService(),
            uiDispatcher: dispatcher);
        var outside = new List<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CatalogueViewModel.IsLoading) or
                                  nameof(CatalogueViewModel.TotalCount) or
                                  nameof(CatalogueViewModel.IsEmpty) or
                                  nameof(CatalogueViewModel.LibraryRootPath) &&
                !RecordingDispatcher.IsInside &&
                !Dispatcher.UIThread.CheckAccess())
            {
                outside.Add(e.PropertyName!);
            }
        };

        await Task.Run(() => viewModel.LoadAsync());
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(outside);
        Assert.Equal(10, viewModel.TotalCount);
        Assert.True(dispatcher.Invocations >= 2);
        viewModel.Dispose();
    }

    private static List<BookSummaryProjection> CreateBooks(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new BookSummaryProjection(
                $"book-{index:D3}",
                $"Topic {index:D3}",
                [$"Author {index:D3}"],
                null,
                0,
                null,
                [],
                null,
                true,
                2000 + index))
            .ToList();

    private sealed class RecordingDispatcher : OgmaLibrary.Application.Diagnostics.IUiDispatcher
    {
        [ThreadStatic]
        private static bool _inside;

        private readonly Lock _gate = new();

        public static bool IsInside => _inside;

        public int Invocations { get; private set; }

        public bool CheckAccess() => _inside;

        public void Post(Action action) => Run(action);

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            Run(action);
            return Task.CompletedTask;
        }

        private void Run(Action action)
        {
            lock (_gate)
            {
                Invocations++;
                _inside = true;
                try
                {
                    action();
                }
                finally
                {
                    _inside = false;
                }
            }
        }
    }

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>Yields between items so overlapping refreshes interleave.</summary>
    private sealed class SlowReadModel(IReadOnlyList<BookSummaryProjection> books) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (BookSummaryProjection book in books)
            {
                await Task.Delay(1, cancellationToken);
                yield return book;
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
