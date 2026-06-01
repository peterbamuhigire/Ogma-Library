using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Providers;

/// <summary>Local-only Ollama chat provider adapter.</summary>
public sealed class OllamaChatProvider : IAiProvider
{
    private static readonly Uri DefaultBaseAddress = new("http://localhost:11434/");

    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of <see cref="OllamaChatProvider"/>.</summary>
    public OllamaChatProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= DefaultBaseAddress;
        EnsureLoopback(_httpClient.BaseAddress);
    }

    /// <inheritdoc />
    public string ProviderKey => "ollama";

    /// <inheritdoc />
    public bool IsLocalOnly => true;

    /// <inheritdoc />
    public async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureLoopback(_httpClient.BaseAddress ?? DefaultBaseAddress);
        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(
                "api/chat",
                new OllamaChatRequest(
                    request.Model,
                    false,
                    [
                        new OllamaMessage("system", "You are Ogma Library's local reading assistant."),
                        new OllamaMessage("user", AiProviderPayload.BuildUserContent(request)),
                    ]),
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        OllamaChatResponse? payload = await JsonSerializer
            .DeserializeAsync<OllamaChatResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string text = payload?.Message?.Content
            ?? throw new InvalidOperationException("Ollama returned no completion text.");
        return new AiCompletion(text, payload.PromptEvalCount, payload.EvalCount, IsLocal: true);
    }

    private static void EnsureLoopback(Uri baseAddress)
    {
        if (!baseAddress.IsLoopback)
        {
            throw new InvalidOperationException("Ollama chat must use a loopback base address.");
        }
    }

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount);
}
