using System.Security.Cryptography;
using System.Text.Json;
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
        string bundlePath = Path.Combine(sourceRoot, "shelf3d.js");
        await File.WriteAllTextAsync(bundlePath, "window.ogmaShelf3D = {};");
        await WriteBuildManifestAsync(sourceRoot, bundlePath);
        var publisher = new Shelf3DAssetPublisher(sourceRoot);

        Uri bootstrapUri = await publisher.PublishAsync(_assetRoot);

        Assert.Equal("ogma://assets/js/index.html", bootstrapUri.ToString());
        Assert.True(File.Exists(Path.Combine(_assetRoot, "js", "index.html")));
        Assert.True(File.Exists(Path.Combine(_assetRoot, "js", "shelf3d.js")));
        Assert.True(File.Exists(Path.Combine(_assetRoot, "js", "shelf3d.build.json")));
    }

    [Fact]
    public async Task Shelf3DAssetPublisher_RejectsBundleTamperingAgainstBuildManifest()
    {
        string sourceRoot = Path.Combine(_assetRoot, "tampered-source");
        Directory.CreateDirectory(sourceRoot);
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "index.html"), "<html></html>");
        string bundlePath = Path.Combine(sourceRoot, "shelf3d.js");
        await File.WriteAllTextAsync(bundlePath, "window.ogmaShelf3D = {};");
        await WriteBuildManifestAsync(sourceRoot, bundlePath);
        await File.AppendAllTextAsync(bundlePath, "// tampered");

        var publisher = new Shelf3DAssetPublisher(sourceRoot);

        await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishAsync(_assetRoot));
        Assert.False(File.Exists(Path.Combine(_assetRoot, "js", "shelf3d.js")));
    }

    private static async Task WriteBuildManifestAsync(string sourceRoot, string bundlePath)
    {
        byte[] bundle = await File.ReadAllBytesAsync(bundlePath);
        var manifest = new
        {
            schema = "ogma-shelf3d-build-v1",
            entryPoint = "src/main.ts",
            sourceFiles = new[] { "src/main.ts" },
            sourceSha256 = new string('a', 64),
            lockfileSha256 = new string('b', 64),
            bundleSha256 = Convert.ToHexStringLower(SHA256.HashData(bundle)),
        };
        await File.WriteAllTextAsync(
            Path.Combine(sourceRoot, "shelf3d.build.json"),
            JsonSerializer.Serialize(manifest));
    }

    public void Dispose()
    {
        if (Directory.Exists(_assetRoot))
        {
            Directory.Delete(_assetRoot, recursive: true);
        }
    }
}
