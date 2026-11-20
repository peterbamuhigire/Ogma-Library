namespace OgmaLibrary.Application.Reader;

/// <summary>
/// One independent reader session and the text-layer service bound to it (Sept-23 Phase 12,
/// READ-012). Disposing the scope releases the pane's PDF worker session.
/// </summary>
public interface IReaderSessionScope : IDisposable
{
    /// <summary>The scope's own reader session; never shared with another pane.</summary>
    IReaderSessionService Sessions { get; }

    /// <summary>The text-layer service that reads through <see cref="Sessions"/>.</summary>
    ITextLayerService TextLayers { get; }
}

/// <summary>
/// Creates scoped reader sessions for split-view panes. The main reader keeps the singleton
/// <see cref="IReaderSessionService"/>; each pane takes its own scope.
/// </summary>
public interface IReaderSessionServiceFactory
{
    /// <summary>Creates a new independent session scope.</summary>
    /// <returns>A scope the caller must dispose.</returns>
    IReaderSessionScope CreateScope();
}
