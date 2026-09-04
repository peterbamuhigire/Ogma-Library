using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Infrastructure.AI;

namespace OgmaLibrary.Infrastructure.AI.Providers;

/// <summary>Creates provider adapters from provider settings.</summary>
public sealed class AiProviderFactory : IAiProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiProviderHealthRegistry _health;

    /// <summary>Initializes a new instance of <see cref="AiProviderFactory"/>.</summary>
    public AiProviderFactory(
        IHttpClientFactory httpClientFactory,
        AiProviderHealthRegistry? health = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
        _health = health ?? new AiProviderHealthRegistry();
    }

    /// <inheritdoc />
    public IAiProvider Create(AiProviderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        IAiProvider provider = binding.ProviderKey.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAi(binding, "openai"),
            "deepseek" => CreateOpenAi(binding, "deepseek"),
            "anthropic" => CreateAnthropic(binding),
            "ollama" => CreateOllama(binding),
            "disabled" => new AiDisabledProvider(),
            _ => throw new NotSupportedException($"AI provider '{binding.ProviderKey}' is not supported."),
        };
        return provider is AiDisabledProvider
            ? provider
            : new ResilientAiProvider(provider, _health);
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
