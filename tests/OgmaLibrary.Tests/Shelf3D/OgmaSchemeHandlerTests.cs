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

    public void Dispose()
    {
        if (Directory.Exists(_assetRoot))
        {
            Directory.Delete(_assetRoot, recursive: true);
        }
    }
}
