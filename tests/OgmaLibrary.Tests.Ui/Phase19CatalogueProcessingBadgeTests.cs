using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Render proof for Phase 19 processing, quality, OCR, and availability badges.</summary>
public sealed class Phase19CatalogueProcessingBadgeTests
{
    [AvaloniaFact]
    public async Task GridAndList_RenderProcessingQualityAndAvailabilityBadges()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new SeededReadModel(
        [
            new BookSummaryProjection(
                "badge-ready",
                "Ready book",
                ["Author"],
                null,
                0,
                null,
                [],
                null,
                true,
                2026,
                Processing: new CatalogueProcessingProjection(
                    SearchBookIndexStatus.Indexed,
                    SearchEmbeddingStatus.Embedded,
                    0.875,
                    IsOcrDerived: true)),
            new BookSummaryProjection(
                "badge-missing",
                "Missing book",
                ["Author"],
                null,
                1,
                null,
                [],
                null,
                false,
                2025),
        ]);
        var viewModel = new CatalogueViewModel(readModel, new NoOpNavigation(), localization);
        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        var window = new Window
        {
            Width = 800,
            Height = 520,
            Content = new CatalogueGridView { DataContext = viewModel },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        Assert.Contains(localization["Catalogue.Badge.Indexed"], text);
        Assert.Contains(localization["Catalogue.Badge.Embedded"], text);
        Assert.Contains(localization["Catalogue.Badge.Ocr"], text);
        Assert.Contains("88%", text);
        Assert.Contains(localization["Catalogue.Badge.Unavailable"], text);

        var listWindow = new Window
        {
            Width = 800,
            Height = 520,
            Content = new CatalogueListView { DataContext = viewModel },
        };
        listWindow.Show();
        Dispatcher.UIThread.RunJobs();
        var listText = listWindow.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        Assert.Contains(localization["Catalogue.Badge.Indexed"], listText);
        Assert.Contains(localization["Catalogue.Badge.Embedded"], listText);
        Assert.Contains(localization["Catalogue.Badge.Ocr"], listText);
        Assert.Contains("88%", listText);
        Assert.Contains(localization["Catalogue.Badge.Unavailable"], listText);
        viewModel.Dispose();
        window.Close();
        listWindow.Close();
    }

    private sealed class NoOpNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SeededReadModel : ICatalogueReadModel
    {
        private readonly IReadOnlyList<BookSummaryProjection> _books;

        public SeededReadModel(IReadOnlyList<BookSummaryProjection> books) => _books = books;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in _books)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
