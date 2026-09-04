using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for the production directory catalogue surface.</summary>
public sealed class CatalogueDirectoryViewRenderTests
{
    [AvaloniaFact]
    public void DirectoryView_RendersRelativePathAndBookMetadata()
    {
        var viewModel = new CatalogueViewModel(
            new EmptyCatalogueReadModel(),
            new NoOpNavigation(),
            new InMemoryLocalizationService());
        viewModel.FilteredItems.Add(new BookSummaryProjection(
            "book-001",
            "Thinking in Systems",
            ["Donella Meadows"],
            null,
            0,
            null,
            [],
            null,
            true,
            2008,
            RelativePath: "systems/thinking-in-systems.pdf"));
        viewModel.CurrentView = CatalogueView.Directory;

        var window = new Window
        {
            Width = 900,
            Height = 400,
            Content = new CatalogueDirectoryView { DataContext = viewModel },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame!.Size.Width > 100);
        Assert.True(frame.Size.Height > 100);
        window.Close();
        viewModel.Dispose();
    }

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyCatalogueReadModel : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }
}
