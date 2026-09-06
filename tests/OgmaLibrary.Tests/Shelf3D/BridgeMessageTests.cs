using System.Security.Cryptography;
using System.Text.Json;
using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Bookshelf3D.Messages;

namespace OgmaLibrary.Tests.Shelf3D;

/// <summary>Phase 14 WebView bridge and message-contract tests.</summary>
public sealed class BridgeMessageTests
{
    private const string ValidBookId = "01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7";

    [Fact]
    public void Phase33ContractFreeze_ProtocolAndMessageFamiliesMatchV1()
    {
        Assert.Equal("shelf3d-v1", BridgeProtocol.CurrentVersion);

        string[] outboundMessages = typeof(OutboundMessage).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(OutboundMessage).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] inboundMessages = typeof(InboundMessage).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(InboundMessage).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                nameof(FocusBookMessage),
                nameof(RemoveBookMessage),
                nameof(SetCameraMessage),
                nameof(SetLayoutMessage),
                nameof(SetSceneMessage),
                nameof(SetThemeMessage),
                nameof(UpdateBookMessage),
            ],
            outboundMessages);
        Assert.Equal(
            [
                nameof(BookClickedMessage),
                nameof(BookDoubleClickedMessage),
                nameof(BookHoveredMessage),
                nameof(CameraChangedMessage),
                nameof(PerformanceMetricsMessage),
                nameof(PerformanceWarningMessage),
                nameof(UnknownInboundMessage),
                nameof(WebGl2StatusMessage),
            ],
            inboundMessages);
    }

    [Fact]
    public async Task WebView2Bridge_PostMessage_SerializesCorrectly()
    {
        var host = new FakeWebViewHostAdapter();
        var bridge = new WebView2Bridge();
        await bridge.InitializeAsync(host, CancellationToken.None);

        await bridge.PostMessageAsync(new SetLayoutMessage("grid3d"), CancellationToken.None);

        Assert.Equal("""{"layout":"grid3d","type":"SetLayout","version":"shelf3d-v1"}""", host.LastPostedJson);
    }

    [Fact]
    public async Task WebView2Bridge_FocusBook_SerializesCorrectly()
    {
        var host = new FakeWebViewHostAdapter();
        var bridge = new WebView2Bridge();
        await bridge.InitializeAsync(host, CancellationToken.None);

        await bridge.PostMessageAsync(new FocusBookMessage(ValidBookId), CancellationToken.None);

        Assert.Equal(
            """{"bookId":"01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7","type":"FocusBook","version":"shelf3d-v1"}""",
            host.LastPostedJson);
    }

    [Fact]
    public async Task WKWebViewBridge_PostMessage_SerializesCorrectly()
    {
        var host = new FakeWebViewHostAdapter();
        var bridge = new WKWebViewBridge();
        await bridge.InitializeAsync(host, CancellationToken.None);

        await bridge.PostMessageAsync(
            new SetSceneMessage(
                [new BookSceneItem(ValidBookId, "Systems", "Ogma Team", "ogma://assets/spines/systems.png", null)],
                new CameraState(0, 1, 3, 0, 0, 0, 45)),
            CancellationToken.None);

        Assert.NotNull(host.LastPostedJson);
        Assert.Contains("\"type\":\"SetScene\"", host.LastPostedJson, StringComparison.Ordinal);
        Assert.Contains("\"bookId\":\"01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7\"", host.LastPostedJson, StringComparison.Ordinal);
        Assert.Contains("\"fov\":45", host.LastPostedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void InboundMessageValidator_BookId_InvalidShape_Rejected()
    {
        InboundMessageValidationResult result = InboundMessageValidator.Validate(new BookClickedMessage("../../secrets.db"));

        Assert.False(result.ShouldDispatch);
        Assert.Contains("BookId", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InboundMessageValidator_CameraState_NonFiniteFloat_Rejected()
    {
        InboundMessageValidationResult result = InboundMessageValidator.Validate(
            new CameraChangedMessage(new CameraState(double.NaN, 0, 0, 0, 0, 0, 45)));

        Assert.False(result.ShouldDispatch);
        Assert.Contains("Camera", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InboundMessageValidator_UnknownType_ReturnsDiscardResult_NoException()
    {
        InboundMessageValidationResult result = InboundMessageValidator.Validate(new UnknownInboundMessage("DeleteEverything"));

        Assert.False(result.ShouldDispatch);
        Assert.Contains("Unknown", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InboundMessageParser_RejectsUnsupportedProtocolVersion()
    {
        bool parsed = InboundMessageParser.TryParse(
            "{\"version\":\"shelf3d-v0\",\"type\":\"BookClicked\",\"bookId\":\"01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7\"}",
            out _,
            out string? error);

        Assert.False(parsed);
        Assert.Contains("version", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InboundMessageParser_AcceptsBoundedPerformanceMetrics()
    {
        bool parsed = InboundMessageParser.TryParse(
            """{"version":"shelf3d-v1","type":"PerformanceMetrics","averageFps":60,"frameTimeMs":16.6,"drawCalls":29,"sceneBookCount":10000,"residentBookCount":500,"reducedMotion":true}""",
            out InboundMessage? message,
            out string? error);

        Assert.True(parsed, error);
        PerformanceMetricsMessage metrics = Assert.IsType<PerformanceMetricsMessage>(message);
        Assert.Equal(10_000, metrics.SceneBookCount);
        Assert.Equal(500, metrics.ResidentBookCount);
        Assert.True(InboundMessageValidator.Validate(metrics).ShouldDispatch);
    }

    [Fact]
    public void InboundMessageValidator_PerformanceMetrics_RejectsResidentOverflow()
    {
        InboundMessageValidationResult result = InboundMessageValidator.Validate(
            new PerformanceMetricsMessage(60, 16, 10, 50, 51, false));

        Assert.False(result.ShouldDispatch);
        Assert.Contains("bounds", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InboundMessage_Invalid_IsRejectedWithNoSideEffect()
    {
        var host = new FakeWebViewHostAdapter();
        var bridge = new WebView2Bridge();
        InboundMessage? received = null;
        bridge.MessageReceived += (_, message) => received = message;
        await bridge.InitializeAsync(host, CancellationToken.None);

        host.Emit("""{"type":"BookClicked","bookId":"../../secrets.db"}""");
        Assert.Null(received);

        host.Emit("""{"type":"BookClicked","bookId":"01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7"}""");

        BookClickedMessage clicked = Assert.IsType<BookClickedMessage>(received);
        Assert.Equal(ValidBookId, clicked.BookId);
    }

    [Fact]
    public async Task Shelf3DWebViewBootstrapper_PublishesAssets_RegistersScheme_AndNavigates()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-bootstrap-{Guid.NewGuid():N}");
        string sourceRoot = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceRoot);
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "index.html"), "<html></html>");
        string bundlePath = Path.Combine(sourceRoot, "shelf3d.js");
        await File.WriteAllTextAsync(bundlePath, "window.ogmaShelf3D = {};");
        await WriteBuildManifestAsync(sourceRoot, bundlePath);
        var host = new FakeWebViewHostAdapter();
        var bridge = new WebView2Bridge();
        var bootstrapper = new Shelf3DWebViewBootstrapper(
            bridge,
            new OgmaSchemeHandler(root),
            new Shelf3DAssetPublisher(sourceRoot),
            root);

        try
        {
            await bootstrapper.InitializeAsync(host, CancellationToken.None);

            Assert.True(host.WasInitialized);
            Assert.Equal("ogma", host.RegisteredScheme);
            Assert.Equal("ogma://assets/js/index.html", host.NavigatedUri?.ToString());
            Assert.True(File.Exists(Path.Combine(root, "js", "index.html")));
            Assert.True(File.Exists(Path.Combine(root, "js", "shelf3d.js")));
            Assert.True(File.Exists(Path.Combine(root, "js", "shelf3d.build.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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


    private sealed class FakeWebViewHostAdapter : IWebViewHostAdapter
    {
        public event EventHandler<string>? RawMessageReceived;

        public string? LastPostedJson { get; private set; }
        public bool WasInitialized { get; private set; }
        public string? RegisteredScheme { get; private set; }
        public Uri? NavigatedUri { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            WasInitialized = true;
            return Task.CompletedTask;
        }

        public Task PostJsonAsync(string json, CancellationToken cancellationToken)
        {
            LastPostedJson = json;
            return Task.CompletedTask;
        }

        public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken) =>
            Task.FromResult("ok");

        public Task RegisterSchemeHandlerAsync(
            string scheme,
            ISchemeHandler handler,
            CancellationToken cancellationToken)
        {
            RegisteredScheme = scheme;
            return Task.CompletedTask;
        }

        public Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
        {
            NavigatedUri = uri;
            return Task.CompletedTask;
        }

        public void Emit(string json) => RawMessageReceived?.Invoke(this, json);
    }
}
