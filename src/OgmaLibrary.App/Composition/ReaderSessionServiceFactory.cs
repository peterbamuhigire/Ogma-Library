using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Reader.TextLayer;

namespace OgmaLibrary.App.Composition;

/// <summary>Builds independent <see cref="ReaderSessionService"/> scopes for split-view panes.</summary>
internal sealed class ReaderSessionServiceFactory : IReaderSessionServiceFactory
{
    private readonly IPdfRendererFactory _renderers;
    private readonly IReadingProgressService _progress;
    private readonly IBookFileLocator _locator;
    private readonly PageRenderCache _cache;

    public ReaderSessionServiceFactory(
        IPdfRendererFactory renderers,
        IReadingProgressService progress,
        IBookFileLocator locator,
        PageRenderCache cache)
    {
        _renderers = renderers;
        _progress = progress;
        _locator = locator;
        _cache = cache;
    }

    public IReaderSessionScope CreateScope()
    {
        var session = new ReaderSessionService(_renderers, _progress, _locator, _cache);
        return new Scope(session, new TextLayerService(_renderers, session));
    }

    private sealed class Scope : IReaderSessionScope
    {
        private readonly ReaderSessionService _session;

        public Scope(ReaderSessionService session, ITextLayerService textLayers)
        {
            _session = session;
            TextLayers = textLayers;
        }

        public IReaderSessionService Sessions => _session;

        public ITextLayerService TextLayers { get; }

        public void Dispose() => _session.Dispose();
    }
}
