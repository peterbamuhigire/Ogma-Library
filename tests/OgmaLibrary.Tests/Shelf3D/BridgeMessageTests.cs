using OgmaLibrary.Bookshelf3D.Assets;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Bookshelf3D.Messages;

namespace OgmaLibrary.Tests.Shelf3D;

/// <summary>Phase 14 WebView bridge and message-contract tests.</summary>
public sealed class BridgeMessageTests
{
    private const string ValidBookId = "01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7";

    [Fact]
    public async Task WebView2Bridge_PostMessage_SerializesCorrectly()
    {
        var host = new FakeWebViewHostAdapter();
        var bridge = new WebView2Bridge();
        await bridge.InitializeAsync(host, CancellationToken.None);

        await bridge.PostMessageAsync(new SetLayoutMessage("grid3d"), CancellationToken.None);

        Assert.Equal("""{"layout":"grid3d","type":"SetLayout"}""", host.LastPostedJson);
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

    private sealed class FakeWebViewHostAdapter : IWebViewHostAdapter
    {
        public event EventHandler<string>? RawMessageReceived;

        public string? LastPostedJson { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void Emit(string json) => RawMessageReceived?.Invoke(this, json);
    }
}
