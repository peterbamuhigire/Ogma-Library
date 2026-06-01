using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.AI.Ollama;

/// <summary>
/// Local-only Ollama embedding adapter. The adapter rejects non-loopback base
/// addresses so semantic indexing cannot accidentally become cloud egress.
/// </summary>
internal sealed class OllamaEmbeddingAdapter : IOllamaEmbeddingProvider
{
    private static readonly Uri DefaultBaseAddress = new("http://localhost:11434");

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="OllamaEmbeddingAdapter"/>.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public OllamaEmbeddingAdapter(HttpClient httpClient)
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
    public async Task<OllamaEmbeddingResult> EmbedAsync(
        string text,
        string modelName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        EnsureLoopback(_httpClient.BaseAddress ?? DefaultBaseAddress);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(
                "/api/embeddings",
                new EmbeddingRequest(modelName, text),
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        EmbeddingResponse? payload = await response.Content
            .ReadFromJsonAsync<EmbeddingResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (payload?.Embedding is null || payload.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned an empty embedding vector.");
        }

        return new OllamaEmbeddingResult(
            modelName,
            payload.Model ?? modelName,
            payload.Embedding);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        EnsureLoopback(_httpClient.BaseAddress ?? DefaultBaseAddress);
        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync("/api/tags", cancellationToken)
                .ConfigureAwait(false);
            return response.StatusCode is HttpStatusCode.OK;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static void EnsureLoopback(Uri baseAddress)
    {
        if (!baseAddress.IsLoopback)
        {
            throw new InvalidOperationException(
                "Ollama embeddings must use a loopback base address.");
        }
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding,
        [property: JsonPropertyName("model")] string? Model);
}
