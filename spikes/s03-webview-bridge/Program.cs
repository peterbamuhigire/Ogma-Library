// Spike 3 — headless validation of the bridge contract (THROWAWAY).
// Proves the typed inbound-message validation (SI-3) works: well-formed known
// events dispatch; malformed JSON and unknown types are rejected and never
// acted upon. The WebView GUI round-trip is a separate, desktop-session check
// recorded in RESULT.md.

using System.Text.Json;
using Ogma.Spikes.WebViewBridge;

var dispatcher = new BridgeDispatcher();
var fired = new List<string>();
dispatcher.BookClicked += id => fired.Add($"clicked:{id}");
dispatcher.BookDoubleClicked += id => fired.Add($"dblclick:{id}");
dispatcher.BookHovered += id => fired.Add($"hover:{id}");
dispatcher.CameraChanged += c => fired.Add($"camera:zoom={c.Zoom}");

int pass = 0, fail = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"  [{(condition ? "PASS" : "FAIL")}] {name}");
    if (condition) pass++; else fail++;
}

Console.WriteLine("Spike 3 — bridge contract validation");

// 1) Valid known events are accepted and dispatched.
Check("bookClicked accepted",
    dispatcher.Dispatch("""{"type":"bookClicked","data":{"bookId":"b_01"}}""") && fired.Contains("clicked:b_01"));
Check("bookDoubleClicked accepted",
    dispatcher.Dispatch("""{"type":"bookDoubleClicked","data":{"bookId":"b_02"}}"""));
Check("bookHovered accepted",
    dispatcher.Dispatch("""{"type":"bookHovered","data":{"bookId":"b_03"}}"""));
Check("cameraChanged accepted",
    dispatcher.Dispatch("""{"type":"cameraChanged","data":{"position":[0,0,5],"target":[0,0,0],"zoom":1.5}}""")
    && fired.Contains("camera:zoom=1.5"));

// 2) Unknown event types are rejected (SI-3) and not acted upon.
int firedBefore = fired.Count;
Check("unknown type rejected",
    !dispatcher.Dispatch("""{"type":"evalArbitraryJs","data":{"x":1}}""") && fired.Count == firedBefore);

// 3) Malformed JSON is rejected without throwing.
Check("malformed json rejected",
    !dispatcher.Dispatch("{ this is not json "));

// 4) Outbound command serialisation produces the expected envelope.
var scene = new SceneModel("warm-oak", "shelf", "b_01", new[]
{
    new SceneBook("b_01", "The Name of the Wind", new[] { "Patrick Rothfuss" },
        "ogma://spines/b_01.webp", "ogma://covers/b_01.webp", "reading", 0.82)
});
var cmd = BridgeDispatcher.Command(OutboundCommandTypes.SetScene, scene);
using var doc = JsonDocument.Parse(cmd);
Check("outbound setScene envelope well-formed",
    doc.RootElement.GetProperty("type").GetString() == "setScene"
    && doc.RootElement.GetProperty("payload").GetProperty("books")[0]
        .GetProperty("spineTexture").GetString()!.StartsWith("ogma://"));

Console.WriteLine($"\nResult: {pass} passed, {fail} failed.");
return fail == 0 ? 0 : 1;
