using OgmaLibrary.Bookshelf3D.Assets;

namespace OgmaLibrary.Tests.Shelf3D;

/// <summary>Phase 14 ogma:// scheme-handler tests.</summary>
public sealed class OgmaSchemeHandlerTests : IDisposable
{
    private readonly string _assetRoot = Path.Combine(Path.GetTempPath(), $"ogma-assets-{Guid.NewGuid():N}");

    [Fact]
    public async Task SchemeHandlerTest_ValidUri_ReturnsImageBytes()
    {
        byte[] expected = [0x89, 0x50, 0x4E, 0x47];
        string spines = Path.Combine(_assetRoot, "spines");
        Directory.CreateDirectory(spines);
        await File.WriteAllBytesAsync(Path.Combine(spines, "book.png"), expected);
        var handler = new OgmaSchemeHandler(_assetRoot);

        SchemeResponse response = await handler.HandleAsync(new Uri("ogma://assets/spines/book.png"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("image/png", response.ContentType);
        Assert.Equal(expected, response.Body);
    }

    [Fact]
    public async Task SchemeHandlerTest_PathTraversal_Returns403()
    {
        Directory.CreateDirectory(Path.Combine(_assetRoot, "covers"));
        var handler = new OgmaSchemeHandler(_assetRoot);

        SchemeResponse response = await handler.HandleAsync(new Uri("ogma://assets/covers/../../secrets.db"));

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task SchemeHandlerTest_UnknownAssetClass_Returns404()
    {
        var handler = new OgmaSchemeHandler(_assetRoot);

        SchemeResponse response = await handler.HandleAsync(new Uri("ogma://assets/private/book.png"));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task SchemeHandlerTest_Javascript_ReturnsApplicationJavascript()
    {
        byte[] expected = "console.log('ogma');"u8.ToArray();
        string js = Path.Combine(_assetRoot, "js");
        Directory.CreateDirectory(js);
        await File.WriteAllBytesAsync(Path.Combine(js, "shelf3d.js"), expected);
        var handler = new OgmaSchemeHandler(_assetRoot);

        SchemeResponse response = await handler.HandleAsync(new Uri("ogma://assets/js/shelf3d.js"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/javascript", response.ContentType);
        Assert.Equal(expected, response.Body);
    }

    [Fact]
    public async Task SchemeHandlerTest_HtmlBootstrap_ReturnsTextHtml()
    {
        byte[] expected = "<!doctype html>"u8.ToArray();
        string js = Path.Combine(_assetRoot, "js");
        Directory.CreateDirectory(js);
        await File.WriteAllBytesAsync(Path.Combine(js, "index.html"), expected);
        var handler = new OgmaSchemeHandler(_assetRoot);

        SchemeResponse response = await handler.HandleAsync(new Uri("ogma://assets/js/index.html"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Equal(expected, response.Body);
    }

    [Fact]
    public async Task Shelf3DAssetPublisher_CopiesBuiltAssetsToOgmaJsRoot()
    {
        string sourceRoot = Path.Combine(_assetRoot, "source");
        Directory.CreateDirectory(sourceRoot);
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "index.html"), "<html></html>");
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "shelf3d.js"), "window.ogmaShelf3D = {};");
        var publisher = new Shelf3DAssetPublisher(sourceRoot);

        Uri bootstrapUri = await publisher.PublishAsync(_assetRoot);

        Assert.Equal("ogma://assets/js/index.html", bootstrapUri.ToString());
        Assert.True(File.Exists(Path.Combine(_assetRoot, "js", "index.html")));
        Assert.True(File.Exists(Path.Combine(_assetRoot, "js", "shelf3d.js")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_assetRoot))
        {
            Directory.Delete(_assetRoot, recursive: true);
        }
    }
}
