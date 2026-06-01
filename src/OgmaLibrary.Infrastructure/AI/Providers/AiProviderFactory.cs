using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Providers;

/// <summary>Creates provider adapters from provider settings.</summary>
public sealed class AiProviderFactory : IAiProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Initializes a new instance of <see cref="AiProviderFactory"/>.</summary>
    public AiProviderFactory(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public IAiProvider Create(AiProviderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return binding.ProviderKey.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAi(binding, "openai"),
            "deepseek" => CreateOpenAi(binding, "deepseek"),
            "anthropic" => CreateAnthropic(binding),
            "ollama" => CreateOllama(binding),
            "disabled" => new AiDisabledProvider(),
            _ => throw new NotSupportedException($"AI provider '{binding.ProviderKey}' is not supported."),
        };
    }

    private OpenAiCompatProvider CreateOpenAi(AiProviderBinding binding, string providerKey)
    {
        HttpClient client = CreateClient(binding);
        return new OpenAiCompatProvider(client, RequireApiKey(binding), providerKey);
    }

    private AnthropicProvider CreateAnthropic(AiProviderBinding binding)
    {
        HttpClient client = CreateClient(binding);
        return new AnthropicProvider(client, RequireApiKey(binding));
    }

    private OllamaChatProvider CreateOllama(AiProviderBinding binding)
    {
        HttpClient client = CreateClient(binding);
        return new OllamaChatProvider(client);
    }

    private HttpClient CreateClient(AiProviderBinding binding)
    {
        HttpClient client = _httpClientFactory.CreateClient($"ai:{binding.ProviderKey.ToLowerInvariant()}");
        if (binding.BaseAddress is not null)
        {
            client.BaseAddress = binding.BaseAddress;
        }

        return client;
    }

    private static string RequireApiKey(AiProviderBinding binding) =>
        string.IsNullOrWhiteSpace(binding.ApiKey)
            ? throw new InvalidOperationException($"AI provider '{binding.ProviderKey}' requires an API key.")
            : binding.ApiKey;
}
