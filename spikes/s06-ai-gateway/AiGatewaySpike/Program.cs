using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// ─── Interface ───────────────────────────────────────────────────────────────
// FR-AI-002 / ADR-0007 – single egress chokepoint; every provider must pass
// through this contract and ONLY through this contract.
public interface IAiProvider
{
    string Name { get; }
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
    Task<bool> ValidateCredentialsAsync(CancellationToken ct);
}

// ─── OpenAI-compatible adapter ───────────────────────────────────────────────
public sealed class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string Name => "OpenAI";

    public OpenAiProvider(string baseUrl, string apiKey, string model = "gpt-4o-mini")
    {
        _model = model;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        // Key sourced from env var by the caller – never stored as a literal
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 64
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? string.Empty;
    }

    public async Task<bool> ValidateCredentialsAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync("/v1/models", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

// ─── Anthropic adapter ───────────────────────────────────────────────────────
public sealed class AnthropicProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string Name => "Anthropic";

    public AnthropicProvider(string apiKey, string model = "claude-3-haiku-20240307")
    {
        _model = model;
        _http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com") };
        // Key sourced from env var – never hardcoded
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = 64,
            messages = new[] { new { role = "user", content = prompt } }
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
                  .GetProperty("content")[0]
                  .GetProperty("text")
                  .GetString() ?? string.Empty;
    }

    public async Task<bool> ValidateCredentialsAsync(CancellationToken ct)
    {
        // Anthropic has no public /models endpoint; we probe with a minimal request.
        // A 400 "credit balance too low" still means the key is structurally valid and
        // the API is reachable – we surface that as a distinct skip reason rather than
        // silently treating it as "no credentials".
        try
        {
            var probe = await CompleteAsync("ping", ct);
            return probe.Length > 0;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("400"))
        {
            // Try to read the error body (already consumed by EnsureSuccessStatusCode)
            Console.WriteLine($"  [Anthropic 400: key is set but API returned Bad Request – likely insufficient credits]");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Anthropic ValidateCredentials error: {ex.GetType().Name}: {ex.Message}]");
            return false;
        }
    }
}

// ─── Ollama adapter ──────────────────────────────────────────────────────────
public sealed class OllamaProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string Name => "Ollama";

    public OllamaProvider(string baseUrl = "http://localhost:11434", string model = "llama3.2")
    {
        _model = model;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            prompt,
            stream = false
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }

    public async Task<bool> ValidateCredentialsAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync("/api/tags", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

// ─── Gateway runner ──────────────────────────────────────────────────────────
// This is the SINGLE method that exercises all providers identically.
// No provider-specific code appears here – only IAiProvider calls.
static class Gateway
{
    public const string TestPrompt = "What is the capital of France?";

    public static async Task RunThroughGateway(IAiProvider provider)
    {
        Console.WriteLine($"\n-- Provider: {provider.Name} --");

        bool credOk;
        var credSw = Stopwatch.StartNew();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            credOk = await provider.ValidateCredentialsAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ValidateCredentials threw: {ex.GetType().Name}: {ex.Message}");
            credOk = false;
        }
        credSw.Stop();

        if (!credOk)
        {
            Console.WriteLine("  Status : skipped (no credentials or endpoint not reachable)");
            return;
        }

        Console.WriteLine($"  Credential check : {credSw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  Prompt           : \"{TestPrompt}\"");

        var sw = Stopwatch.StartNew();
        string response;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            response = await provider.CompleteAsync(TestPrompt, cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CompleteAsync threw: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        sw.Stop();

        Console.WriteLine($"  Response         : \"{response.Trim()}\"");
        Console.WriteLine($"  Round-trip       : {sw.ElapsedMilliseconds} ms");
        Console.WriteLine("  Live call made   : YES");
    }
}

// ─── Entry point ─────────────────────────────────────────────────────────────
class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Ogma Library - Spike 6: AI Gateway ===");
        Console.WriteLine($"Timestamp: {DateTimeOffset.UtcNow:O}");
        Console.WriteLine($"Runtime  : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

        // Collect providers – keys ONLY from environment variables
        var providers = new List<IAiProvider>();

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            providers.Add(new OpenAiProvider("https://api.openai.com", openAiKey));
        }
        else
        {
            Console.WriteLine("\n-- Provider: OpenAI --");
            Console.WriteLine("  Status : skipped (OPENAI_API_KEY not set)");
        }

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(anthropicKey))
        {
            Console.WriteLine("\n  [ANTHROPIC_API_KEY is set; will attempt live call]");
            providers.Add(new AnthropicProvider(anthropicKey));
        }
        else
        {
            Console.WriteLine("\n-- Provider: Anthropic --");
            Console.WriteLine("  Status : skipped (ANTHROPIC_API_KEY not set)");
        }

        // Ollama: no credentials needed; probe the endpoint to decide
        var ollamaProvider = new OllamaProvider();
        bool ollamaReachable;
        try
        {
            using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            ollamaReachable = await ollamaProvider.ValidateCredentialsAsync(probeCts.Token);
        }
        catch
        {
            ollamaReachable = false;
        }

        if (ollamaReachable)
        {
            providers.Add(ollamaProvider);
        }
        else
        {
            Console.WriteLine("\n-- Provider: Ollama --");
            Console.WriteLine("  Status : Ollama not reachable at localhost:11434, round-trip deferred");
        }

        // Run the SAME gateway method for every configured provider – no provider-specific
        // code leaks past the IAiProvider interface boundary.
        foreach (var p in providers)
        {
            await Gateway.RunThroughGateway(p);
        }

        // ─── Privacy-tier note (design only) ─────────────────────────────
        Console.WriteLine("""

=== Privacy-Tier + Payload Preview Design Note (10-line summary) ===
The IAiProvider interface is the single egress chokepoint (FR-AI-002 / ADR-0007).
Four privacy tiers layer above it via a GatewayRouter wrapper:
  Tier 0 LOCAL_ONLY  - route exclusively to OllamaProvider; zero cloud egress.
  Tier 1 ANONYMISED  - PayloadSanitiser.Sanitise(prompt) runs before any CompleteAsync.
  Tier 2 CLOUD_OK    - OpenAiProvider/AnthropicProvider allowed; user consent checked.
  Tier 3 PREVIEW     - IPayloadConfirmationUi modal shown (first 200 chars) before send.
All tiers share RunThroughGateway(IAiProvider); routing and sanitisation live in
GatewayRouter only, keeping provider adapters free of policy logic, and making
the egress chokepoint fully auditable at one call site. No user data in this spike.
""");

        Console.WriteLine("=== Spike 6 complete ===");
    }
}
