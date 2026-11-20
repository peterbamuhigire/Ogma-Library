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
    /// <summary>Ollama model the adapter requests.</summary>
    internal const string DefaultModelId = "nomic-embed-text";

    /// <summary>
    /// Version stamped on stored vectors. It matches the value earlier
    /// releases stored, so existing Ollama vectors remain usable.
    /// </summary>
    internal const string DefaultModelVersion = "nomic-embed-text:latest";

    /// <summary>Output dimension of <c>nomic-embed-text</c>.</summary>
    internal const int DefaultDimensions = 768;

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
    public string ModelId => DefaultModelId;

    /// <inheritdoc />
    public string ModelVersion => DefaultModelVersion;

    /// <inheritdoc />
    public int Dimensions => DefaultDimensions;

    /// <inheritdoc />
    /// <remarks>
    /// The query and passage roles are embedded identically, as before this
    /// contract existed, so vectors already stored for this model stay valid.
    /// </remarks>
    public async Task<EmbeddingResult> EmbedAsync(
        string text,
        EmbeddingInputKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLoopback(_httpClient.BaseAddress ?? DefaultBaseAddress);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(
                "/api/embeddings",
                new EmbeddingRequest(DefaultModelId, text),
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

        if (payload.Embedding.Length != DefaultDimensions)
        {
            throw new InvalidOperationException("Ollama returned an embedding with an unexpected dimension.");
        }

        return new EmbeddingResult(
            ProviderKey,
            DefaultModelId,
            DefaultModelVersion,
            Normalize(payload.Embedding));
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

    private static float[] Normalize(float[] vector)
    {
        double sum = 0;
        foreach (float value in vector)
        {
            if (!float.IsFinite(value))
            {
                throw new InvalidOperationException("Ollama returned a non-finite embedding value.");
            }

            sum += (double)value * value;
        }

        if (sum <= 0)
        {
            throw new InvalidOperationException("Ollama returned a zero embedding vector.");
        }

        double norm = Math.Sqrt(sum);
        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = (float)(vector[i] / norm);
        }

        return normalized;
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
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
