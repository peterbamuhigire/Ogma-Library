using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for the Phase 20 collection-management controls.</summary>
public sealed class ShelfSidebarTests
{
    [AvaloniaFact]
    public async Task CollectionEditor_CreatesAndDeletesThroughTheWriteBoundary()
    {
        var readModel = new ShelfReadModel();
        var writeService = new ShelfWriteService(readModel);
        var filter = new CatalogueFilterViewModel();
        var viewModel = new ShelfSidebarViewModel(
            readModel,
            writeService,
            new InMemoryLocalizationService(),
            filter);

        viewModel.NewShelfName = "  Reading queue  ";
        await viewModel.CreateNewShelfAsync();
        Dispatcher.UIThread.RunJobs();

        ShelfProjection shelf = Assert.Single(viewModel.Shelves);
        Assert.Equal("Reading queue", shelf.Name);
        Assert.Equal("Collection created.", viewModel.StatusText);
        Assert.Empty(viewModel.NewShelfName);

        viewModel.SelectedShelf = shelf;
        Assert.True(viewModel.CanDeleteSelectedShelf);
        viewModel.NewShelfName = "Reading list";
        Assert.True(viewModel.CanRenameSelectedShelf);
        await viewModel.RenameSelectedShelfAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Reading list", Assert.Single(viewModel.Shelves).Name);
        Assert.Equal("Collection renamed.", viewModel.StatusText);

        ShelfProjection renamedShelf = Assert.Single(viewModel.Shelves);
        viewModel.SelectedShelf = renamedShelf;
        await viewModel.DeleteSelectedShelfAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(viewModel.Shelves);
        Assert.Null(viewModel.SelectedShelf);
        Assert.Equal("Collection deleted.", viewModel.StatusText);
        Assert.Equal(1, writeService.CreatedCount);
        Assert.Equal(1, writeService.DeletedCount);
    }

    private sealed class ShelfReadModel : ICatalogueReadModel
    {
        public List<ShelfProjection> Shelves { get; } = [];

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (ShelfProjection shelf in Shelves.ToArray())
            {
                yield return shelf;
            }

            await Task.CompletedTask;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class ShelfWriteService(ShelfReadModel readModel) : ICatalogueWriteService
    {
        public int CreatedCount { get; private set; }
        public int DeletedCount { get; private set; }

        public Task<string> CreateShelfAsync(
            string name,
            bool isSmart = false,
            string? query = null,
            CancellationToken cancellationToken = default)
        {
            CreatedCount++;
            string shelfId = $"shelf-{CreatedCount}";
            readModel.Shelves.Add(new ShelfProjection(shelfId, name, isSmart, 0));
            return Task.FromResult(shelfId);
        }

        public Task RenameShelfAsync(
            string shelfId,
            string newName,
            CancellationToken cancellationToken = default)
        {
            int index = readModel.Shelves.FindIndex(shelf => shelf.ShelfId == shelfId);
            if (index >= 0)
            {
                readModel.Shelves[index] = readModel.Shelves[index] with { Name = newName };
            }
            return Task.CompletedTask;
        }

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default)
        {
            DeletedCount++;
            readModel.Shelves.RemoveAll(shelf => shelf.ShelfId == shelfId);
            return Task.CompletedTask;
        }

        public Task AddBookToShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateMetadataFieldAsync(
            string bookId,
            string fieldName,
            string? value,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
