using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// A deterministic mock <see cref="IPdfRenderer"/> for tests. Returns synthetic
/// PNG bytes (1x1 white pixel) and predictable text layers.
/// </summary>
public sealed class MockPdfRenderer : IPdfRenderer
{
    private static readonly byte[] OnePxPng = CreateOnePxPng();

    /// <summary>Tracks render calls for assertion.</summary>
    public List<(int PageIndex, RenderRequest Request)> RenderCalls { get; } = new();

    /// <inheritdoc />
    public int PageCount { get; }

    /// <summary>Simulate a delay on each render (ms). Default 0 = instant.</summary>
    public int RenderDelayMs { get; set; }

    /// <summary>Whether this renderer has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Words per page to include in the text layer.</summary>
    public IReadOnlyDictionary<int, string[]> PageWords { get; set; } =
        new Dictionary<int, string[]>();

    /// <summary>PDF-standard page rotations by zero-based page index.</summary>
    public IReadOnlyDictionary<int, int> PageRotations { get; set; } =
        new Dictionary<int, int>();

    /// <summary>
    /// Creates a mock renderer with the given page count.
    /// </summary>
    /// <param name="pageCount">Number of pages in the mock document.</param>
    public MockPdfRenderer(int pageCount = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        PageCount = pageCount;
    }

    /// <inheritdoc />
    public async Task<RenderResult> RenderPageAsync(int pageIndex, RenderRequest request, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ct.ThrowIfCancellationRequested();

        if (RenderDelayMs > 0)
        {
            await Task.Delay(RenderDelayMs, ct).ConfigureAwait(false);
        }

        lock (RenderCalls)
        {
            RenderCalls.Add((pageIndex, request));
        }

        return new RenderResult(OnePxPng, 595.0, 842.0, pageIndex);
    }

    /// <inheritdoc />
    public int GetPageRotationDegrees(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return PageRotations.TryGetValue(pageIndex, out int rotation)
            ? ((rotation % 360) + 360) % 360
            : 0;
    }

    /// <inheritdoc />
    public TextLayer ExtractTextLayer(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (PageWords.TryGetValue(pageIndex, out string[]? words) && words.Length > 0)
        {
            var textWords = words.Select((w, i) => new TextWord(
                w,
                Left: i * 0.1,
                Top: 0.1,
                Right: (i * 0.1) + 0.09,
                Bottom: 0.2)).ToList();
            return new TextLayer(pageIndex, textWords, ExtractionQuality.Full);
        }

        return new TextLayer(pageIndex, [], ExtractionQuality.Empty);
    }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;

    private static byte[] CreateOnePxPng()
    {
        // Minimal valid 1x1 white PNG
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    }
}

/// <summary>
/// A mock <see cref="IPdfRendererFactory"/> for tests.
/// </summary>
public sealed class MockPdfRendererFactory : IPdfRendererFactory
{
    private readonly Func<string, MockPdfRenderer> _factory;

    /// <summary>All renderers created by this factory.</summary>
    public List<MockPdfRenderer> CreatedRenderers { get; } = new();

    /// <summary>The last password supplied to the password-aware open path.</summary>
    public char[]? LastPassword { get; private set; }

    /// <summary>
    /// Creates a factory that returns renderers with the given page count.
    /// </summary>
    /// <param name="pageCount">Pages per document.</param>
    public MockPdfRendererFactory(int pageCount = 10)
    {
        _factory = _ => new MockPdfRenderer(pageCount);
    }

    /// <summary>
    /// Creates a factory that produces renderers from the supplied delegate.
    /// </summary>
    /// <param name="factory">Delegate that creates a renderer for a given file path.</param>
    public MockPdfRendererFactory(Func<string, MockPdfRenderer> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc />
    public IPdfRenderer Open(string filePath)
    {
        var renderer = _factory(filePath);
        CreatedRenderers.Add(renderer);
        return renderer;
    }

    /// <inheritdoc />
    public IPdfRenderer Open(string filePath, char[] password)
    {
        ArgumentNullException.ThrowIfNull(password);
        LastPassword = password.ToArray();
        return Open(filePath);
    }
}
