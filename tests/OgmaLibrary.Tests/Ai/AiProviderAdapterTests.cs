using System.Net;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.AI;
using OgmaLibrary.Infrastructure.AI.Providers;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 provider adapter contract tests using stubbed HTTP.</summary>
public sealed class AiProviderAdapterTests
{
    [Fact]
    public async Task OpenAiCompatProvider_PostsChatCompletionAndMapsUsage()
    {
        var handler = new RecordingHandler(
            """{"choices":[{"message":{"content":"Read this next."}}],"usage":{"prompt_tokens":11,"completion_tokens":7}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test/v1/") };
        var provider = new OpenAiCompatProvider(http, "test-key");

        AiCompletion completion = await provider.CompleteAsync(CreateRequest(), CancellationToken.None);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody);

        Assert.Equal("Read this next.", completion.Text);
        Assert.Equal(11, completion.PromptTokens);
        Assert.Equal(7, completion.CompletionTokens);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", handler.Request.Headers.Authorization?.Parameter);
        Assert.Equal("https://api.openai.test/v1/chat/completions", handler.Request.RequestUri?.ToString());
        Assert.Equal("gpt-test", body.RootElement.GetProperty("model").GetString());
        Assert.Contains("Ogma Library", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnthropicProvider_SendsNoTrainingAndCacheControlHeaders()
    {
        var handler = new RecordingHandler(
            """{"content":[{"type":"text","text":"Anthropic answer."}],"usage":{"input_tokens":20,"output_tokens":8,"cache_creation_input_tokens":3,"cache_read_input_tokens":5}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.test/v1/") };
        var provider = new AnthropicProvider(http, "anthropic-key");

        AiCompletion completion = await provider.CompleteAsync(CreateRequest("claude-test"), CancellationToken.None);

        Assert.Equal("Anthropic answer.", completion.Text);
        Assert.Equal(20, completion.PromptTokens);
        Assert.Equal(8, completion.CompletionTokens);
        Assert.Equal(8, completion.PromptCacheTokens);
        Assert.Equal("anthropic-key", Assert.Single(handler.Request.Headers.GetValues("x-api-key")));
        Assert.Equal("1", Assert.Single(handler.Request.Headers.GetValues("X-Anthropic-No-Training")));
        Assert.Contains("\"cache_control\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal("https://api.anthropic.test/v1/messages", handler.Request.RequestUri?.ToString());
    }

    [Fact]
    public async Task OllamaChatProvider_PostsLoopbackChatAndMarksCompletionLocal()
    {
        var handler = new RecordingHandler(
            """{"message":{"role":"assistant","content":"Local answer."},"prompt_eval_count":13,"eval_count":9}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var provider = new OllamaChatProvider(http);

        AiCompletion completion = await provider.CompleteAsync(
            CreateRequest("llama3.2", AiPrivacyTier.LocalOllama),
            CancellationToken.None);

        Assert.Equal("Local answer.", completion.Text);
        Assert.Equal(13, completion.PromptTokens);
        Assert.Equal(9, completion.CompletionTokens);
        Assert.True(completion.IsLocal);
        Assert.Equal("http://localhost:11434/api/chat", handler.Request.RequestUri?.ToString());
        Assert.Contains("\"stream\":false", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void OllamaChatProvider_RejectsNonLoopbackBaseAddress()
    {
        using var http = new HttpClient(new RecordingHandler("{}"))
        {
            BaseAddress = new Uri("https://example.com/"),
        };

        Assert.Throws<InvalidOperationException>(() => new OllamaChatProvider(http));
    }

    [Fact]
    public void AiProviderFactory_RequiresCloudApiKeyAndCreatesDisabledProvider()
    {
        var factory = new AiProviderFactory(new StubHttpClientFactory());

        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(new AiProviderBinding("openai", "gpt-test")));

        IAiProvider disabled = factory.Create(new AiProviderBinding("disabled", "none"));
        Assert.IsType<AiDisabledProvider>(disabled);
    }

    private static AiRequest CreateRequest(
        string model = "gpt-test",
        AiPrivacyTier tier = AiPrivacyTier.MetadataOnly) =>
        new(
            tier,
            tier == AiPrivacyTier.LocalOllama ? "ollama" : "openai",
            model,
            "recommendation",
            "What should I read next?",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ogma Library",
                ["author"] = "Chwezi Core Systems",
            },
            tier == AiPrivacyTier.LocalOllama
                ? [new AiContentChunk("book-1", "page:1", "local chunk")]
                : []);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _response;

        public RecordingHandler(string response)
        {
            _response = response;
        }

        public HttpRequestMessage Request { get; private set; } = new(HttpMethod.Get, "http://localhost/");

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new RecordingHandler("{}")) { BaseAddress = new Uri("http://localhost:11434/") };
    }
}
