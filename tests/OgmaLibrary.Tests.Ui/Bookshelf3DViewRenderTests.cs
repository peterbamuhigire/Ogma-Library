using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.App.Views.Shelf3D;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Bookshelf3D.Messages;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless render tests for the Phase 14 Bookshelf3D shell.</summary>
public sealed class Bookshelf3DViewRenderTests
{
    [AvaloniaFact]
    public async Task Bookshelf3DView_RendersFallbackCatalogue()
    {
        var bridge = new FakeBridge();
        using var viewModel = new Bookshelf3DViewModel(
            new FakeCatalogueReadModel(),
            bridge,
            new RecordingNavigation());
        await viewModel.LoadAsync();
        bridge.Emit(new WebGl2StatusMessage(false));
        Dispatcher.UIThread.RunJobs();

        var window = new Window
        {
            Width = 900,
            Height = 600,
            Content = new Bookshelf3DView { DataContext = viewModel },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame!.Size.Width > 100);
        Assert.True(viewModel.IsFallbackVisible);
        Assert.Single(viewModel.Books);
        window.Close();
    }

    private sealed class FakeBridge : IWebViewBridge
    {
        public event EventHandler<InboundMessage>? MessageReceived;

        public Task InitializeAsync(IWebViewHostAdapter host, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PostMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
                null,
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
