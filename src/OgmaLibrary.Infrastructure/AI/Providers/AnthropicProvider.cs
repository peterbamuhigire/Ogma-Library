using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Providers;

/// <summary>Anthropic Messages API provider adapter with no-training and prompt-cache headers.</summary>
public sealed class AnthropicProvider : IAiProvider
{
    private static readonly Uri DefaultBaseAddress = new("https://api.anthropic.com/v1/");

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    /// <summary>Initializes a new instance of <see cref="AnthropicProvider"/>.</summary>
    public AnthropicProvider(HttpClient httpClient, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _httpClient = httpClient;
        _apiKey = apiKey;
        _httpClient.BaseAddress ??= DefaultBaseAddress;
    }

    /// <inheritdoc />
    public string ProviderKey => "anthropic";

    /// <inheritdoc />
    public bool IsLocalOnly => false;

    /// <inheritdoc />
    public async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "messages");
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Add("X-Anthropic-No-Training", "1");
        httpRequest.Content = JsonContent.Create(new AnthropicMessageRequest(
            request.Model,
            1024,
            [new AnthropicTextBlock("text", "Ogma Library reading assistant context.", new CacheControl("ephemeral"))],
            [new AnthropicMessage("user", [new AnthropicTextBlock("text", AiProviderPayload.BuildUserContent(request), new CacheControl("ephemeral"))])]));

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        AnthropicMessageResponse? payload = await JsonSerializer
            .DeserializeAsync<AnthropicMessageResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? text = FindText(payload?.Content);
        if (string.IsNullOrWhiteSpace(text) || payload?.Usage is null)
        {
            throw new InvalidOperationException("Anthropic provider returned no completion text.");
        }

        AnthropicUsage usage = payload.Usage;
        int? promptCacheTokens = SumNullable(usage.CacheCreationInputTokens, usage.CacheReadInputTokens);
        return new AiCompletion(
            text,
            usage.InputTokens,
            usage.OutputTokens,
            promptCacheTokens);
    }

    private static int? SumNullable(int? first, int? second) =>
        first is null && second is null ? null : first.GetValueOrDefault() + second.GetValueOrDefault();

    private static string? FindText(IReadOnlyList<AnthropicContentBlock>? blocks)
    {
        if (blocks is null)
        {
            return null;
        }

        foreach (AnthropicContentBlock block in blocks)
        {
            if (block.Type == "text" && !string.IsNullOrWhiteSpace(block.Text))
            {
                return block.Text;
            }
        }

        return null;
    }

    private sealed record AnthropicMessageRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] IReadOnlyList<AnthropicTextBlock> System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicTextBlock> Content);

    private sealed record AnthropicTextBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("cache_control")] CacheControl? CacheControl = null);

    private sealed record CacheControl([property: JsonPropertyName("type")] string Type);

    private sealed record AnthropicMessageResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock>? Content,
        [property: JsonPropertyName("usage")] AnthropicUsage? Usage);

    private sealed record AnthropicContentBlock(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int? InputTokens,
        [property: JsonPropertyName("output_tokens")] int? OutputTokens,
        [property: JsonPropertyName("cache_creation_input_tokens")] int? CacheCreationInputTokens,
        [property: JsonPropertyName("cache_read_input_tokens")] int? CacheReadInputTokens);
}
