// Spike 3 — WebView <-> C# typed message bridge contract (THROWAWAY).
// This is the bridge-contract skeleton that feeds the Phase 14 3D bookshelf.
// It defines the typed envelope used in BOTH directions and a validating
// dispatcher. Runtime validation (WebView2 on Windows, WKWebView on macOS)
// is performed in a desktop session — see RESULT.md.
//
// Design intent (HLD §6.2): C# -> JS sends a compact scene model; JS -> C#
// raises a small, closed set of typed interaction events. Every inbound
// message is validated against the contract before the native side acts on it
// (SI-3). Asset textures are referenced only through the safe `ogma://` scheme,
// never raw file-system paths.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ogma.Spikes.WebViewBridge;

/// <summary>Direction: C# → JavaScript. A command the native side sends to the scene.</summary>
public sealed record BridgeCommand(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] JsonElement Payload);

/// <summary>Direction: JavaScript → C#. An interaction event raised by the scene.</summary>
public sealed record BridgeEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] JsonElement Data);

/// <summary>The closed set of inbound (JS → C#) event types the host honours. SI-3.</summary>
public static class InboundEventTypes
{
    public const string BookClicked = "bookClicked";
    public const string BookDoubleClicked = "bookDoubleClicked";
    public const string BookHovered = "bookHovered";
    public const string CameraChanged = "cameraChanged";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        BookClicked, BookDoubleClicked, BookHovered, CameraChanged
    };
}

/// <summary>The outbound (C# → JS) command types.</summary>
public static class OutboundCommandTypes
{
    public const string SetScene = "setScene";       // full scene model
    public const string SelectBook = "selectBook";   // highlight a book by id
    public const string SetTheme = "setTheme";       // theme/layout change
}

/// <summary>Compact scene model handed to the WebView (projection of the catalogue).</summary>
public sealed record SceneModel(
    [property: JsonPropertyName("theme")] string Theme,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("selectedBookId")] string? SelectedBookId,
    [property: JsonPropertyName("books")] IReadOnlyList<SceneBook> Books);

public sealed record SceneBook(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("authors")] IReadOnlyList<string> Authors,
    // ogma:// scheme only — never a raw file path.
    [property: JsonPropertyName("spineTexture")] string SpineTexture,
    [property: JsonPropertyName("coverTexture")] string CoverTexture,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("aiScore")] double AiScore);

/// <summary>
/// Validates and dispatches inbound messages. The host wires
/// <see cref="Dispatch"/> to the WebView's "message received" callback.
/// Unknown types are rejected (never acted upon) per SI-3.
/// </summary>
public sealed class BridgeDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public event Action<string>? BookClicked;
    public event Action<string>? BookDoubleClicked;
    public event Action<string>? BookHovered;
    public event Action<CameraState>? CameraChanged;

    /// <summary>Returns true if the message was a recognised, well-formed event.</summary>
    public bool Dispatch(string rawJson)
    {
        BridgeEvent? evt;
        try { evt = JsonSerializer.Deserialize<BridgeEvent>(rawJson, Json); }
        catch (JsonException) { return false; } // malformed -> rejected

        if (evt is null || !InboundEventTypes.All.Contains(evt.Type))
            return false; // unknown type -> rejected (SI-3)

        switch (evt.Type)
        {
            case InboundEventTypes.BookClicked:
                BookClicked?.Invoke(evt.Data.GetProperty("bookId").GetString() ?? "");
                return true;
            case InboundEventTypes.BookDoubleClicked:
                BookDoubleClicked?.Invoke(evt.Data.GetProperty("bookId").GetString() ?? "");
                return true;
            case InboundEventTypes.BookHovered:
                BookHovered?.Invoke(evt.Data.GetProperty("bookId").GetString() ?? "");
                return true;
            case InboundEventTypes.CameraChanged:
                CameraChanged?.Invoke(evt.Data.Deserialize<CameraState>(Json)!);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Serialise an outbound command for the WebView (window.ogmaReceive).</summary>
    public static string Command(string type, object payload) =>
        JsonSerializer.Serialize(new { type, payload }, Json);
}

public sealed record CameraState(
    [property: JsonPropertyName("position")] double[] Position,
    [property: JsonPropertyName("target")] double[] Target,
    [property: JsonPropertyName("zoom")] double Zoom);
