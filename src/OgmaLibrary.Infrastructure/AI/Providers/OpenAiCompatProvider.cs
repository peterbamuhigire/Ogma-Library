using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Providers;

/// <summary>OpenAI-compatible chat-completions provider adapter.</summary>
public sealed class OpenAiCompatProvider : IAiProvider
{
    private static readonly Uri DefaultBaseAddress = new("https://api.openai.com/v1/");

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    /// <summary>Initializes a new instance of <see cref="OpenAiCompatProvider"/>.</summary>
    public OpenAiCompatProvider(HttpClient httpClient, string apiKey, string providerKey = "openai")
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        _httpClient = httpClient;
        _apiKey = apiKey;
        ProviderKey = providerKey;
        _httpClient.BaseAddress ??= DefaultBaseAddress;
    }

    /// <inheritdoc />
    public string ProviderKey { get; }

    /// <inheritdoc />
    public bool IsLocalOnly => false;

    /// <inheritdoc />
    public async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = JsonContent.Create(new ChatCompletionRequest(
            request.Model,
            [
                new ChatMessage("system", "You are Ogma Library's reading assistant. Be concise, privacy-aware, and useful."),
                new ChatMessage("user", AiProviderPayload.BuildUserContent(request)),
            ],
            1024,
            0.2));

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        OpenAiChatResponse? payload = await JsonSerializer
            .DeserializeAsync<OpenAiChatResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? text = payload?.Choices is { Count: > 0 }
            ? payload.Choices[0].Message?.Content
            : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("OpenAI-compatible provider returned no completion text.");
        }

        Usage? usage = payload?.Usage;
        return new AiCompletion(
            text,
            usage?.PromptTokens,
            usage?.CompletionTokens);
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<Choice>? Choices,
        [property: JsonPropertyName("usage")] Usage? Usage);

    private sealed record Choice([property: JsonPropertyName("message")] ChoiceMessage? Message);

    private sealed record ChoiceMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record Usage(
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens);
}
