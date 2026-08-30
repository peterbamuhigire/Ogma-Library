using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Bookshelf3D.Messages;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Shelf3D;

/// <summary>Phase 14 Bookshelf3D view-model tests.</summary>
public sealed class Bookshelf3DViewModelTests
{
    [Fact]
    public async Task Bookshelf3DViewModel_LoadAsync_PostsSetScene()
    {
        var bridge = new FakeBridge();
        using var viewModel = new Bookshelf3DViewModel(
            new FakeCatalogueReadModel(),
            bridge,
            new RecordingNavigation(),
            new InMemoryLocalizationService());

        await viewModel.LoadAsync();

        SetSceneMessage scene = Assert.IsType<SetSceneMessage>(Assert.Single(bridge.PostedMessages));
        BookSceneItem book = Assert.Single(scene.Books);
        Assert.Equal("01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7", book.BookId);
        Assert.Equal("Thinking in Systems", book.Title);
        Assert.StartsWith("ogma://assets/spines/", book.SpineUri, StringComparison.Ordinal);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void Bookshelf3DViewModel_BookClicked_NavigatesToCorrectBook()
    {
        var bridge = new FakeBridge();
        var navigation = new RecordingNavigation();
        using var viewModel = CreateViewModel(bridge, navigation);

        bridge.Emit(new BookClickedMessage("01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7"));

        Assert.Equal("01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7", navigation.OpenedBookId);
    }

    [Fact]
    public void WebGL2Absent_FallbackFlag_IsSet()
    {
        var bridge = new FakeBridge();
        using var viewModel = CreateViewModel(bridge, new RecordingNavigation());

        bridge.Emit(new WebGl2StatusMessage(false));

        Assert.False(viewModel.IsWebGl2Supported);
        Assert.True(viewModel.IsFallbackVisible);
    }

    [Fact]
    public async Task Bookshelf3DViewModel_WithoutNativeHost_PreservesAccessibleFallback()
    {
        using var viewModel = new Bookshelf3DViewModel(
            new FakeCatalogueReadModel(),
            new WebView2Bridge(),
            new RecordingNavigation(),
            new InMemoryLocalizationService());

        await viewModel.LoadAsync();

        Assert.True(viewModel.IsFallbackVisible);
        Assert.Single(viewModel.Books);
    }

    [Fact]
    public async Task Bookshelf3DViewModel_SetLayout_PostsLayoutMessage()
    {
        var bridge = new FakeBridge();
        using var viewModel = CreateViewModel(bridge, new RecordingNavigation());

        await viewModel.SetLayoutAsync("grid3d");

        SetLayoutMessage layout = Assert.IsType<SetLayoutMessage>(Assert.Single(bridge.PostedMessages));
        Assert.Equal("grid3d", layout.Layout);
        Assert.Equal("grid3d", viewModel.CurrentLayout);
    }

    [Fact]
    public void Bookshelf3DViewModel_Labels_AreLocalized()
    {
        var localization = new InMemoryLocalizationService();
        using var viewModel = new Bookshelf3DViewModel(
            new FakeCatalogueReadModel(),
            new FakeBridge(),
            new RecordingNavigation(),
            localization);

        Assert.Equal("3D Bookshelf", viewModel.Title);
        Assert.Contains("ic_shelf3d_toggle", viewModel.ToggleIconPath, StringComparison.Ordinal);

        localization.SetCulture("fr");

        Assert.Equal("Bibliotheque 3D", viewModel.Title);
        Assert.Equal("Grille 3D", viewModel.GridLayoutLabel);
    }

    private static Bookshelf3DViewModel CreateViewModel(FakeBridge bridge, RecordingNavigation navigation) =>
        new(new FakeCatalogueReadModel(), bridge, navigation, new InMemoryLocalizationService());

    private sealed class FakeBridge : IWebViewBridge
    {
        public event EventHandler<InboundMessage>? MessageReceived;

        public List<OutboundMessage> PostedMessages { get; } = [];

        public Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PostMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        {
            PostedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default) =>
            Task.FromResult("ok");

        public Task RegisterSchemeHandlerAsync(
            string scheme,
            ISchemeHandler handler,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Emit(InboundMessage message) => MessageReceived?.Invoke(this, message);
    }

    private sealed class RecordingNavigation : IBookDetailNavigationService
    {
        public string? OpenedBookId { get; private set; }

        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default)
        {
            OpenedBookId = bookId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCatalogueReadModel : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new BookSummaryProjection(
                "01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7",
                "Thinking in Systems",
                ["Donella Meadows"],
                "covers/thinking.jpg",
                0,
                null,
                ["systems"],
                null,
                true,
                2008);
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
