using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Infrastructure.AI.Advisor;
using OgmaLibrary.Infrastructure.AI.Providers;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Dependency-injection registration helpers for Phase 12 AI gateway services.</summary>
public static class AiServiceExtensions
{
    /// <summary>Registers provider-neutral AI gateway support services.</summary>
    public static IServiceCollection AddAiGatewayCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAiPayloadBuilder, AiPayloadBuilder>();
        services.AddSingleton<IAiCostCalculator, AiCostCalculator>();
        services.AddSingleton<IAiCostFormatter, AiCostFormatter>();
        services.AddSingleton<IAiPrivacyService, AiPrivacyService>();
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
        services.AddSingleton<IAdvisorCatalogueReader, AdvisorCatalogueReader>();
        services.AddSingleton<IMetadataPayloadEnricher, MetadataPayloadEnricher>();
        services.AddSingleton<IRecommendationResponseParser, RecommendationResponseParser>();
        services.AddSingleton<IRecommendationProvenanceValidator, RecommendationProvenanceValidator>();
        services.AddSingleton<IRecommendationStructuralValidator, RecommendationStructuralValidator>();
        services.AddSingleton<IRecommendationPipeline, RecommendationPipeline>();
        services.AddHttpClient("ai:openai", client => client.BaseAddress = new Uri("https://api.openai.com/v1/"));
        services.AddHttpClient("ai:deepseek", client => client.BaseAddress = new Uri("https://api.deepseek.com/v1/"));
        services.AddHttpClient("ai:anthropic", client => client.BaseAddress = new Uri("https://api.anthropic.com/v1/"));
        services.AddHttpClient("ai:ollama", client => client.BaseAddress = new Uri("http://localhost:11434/"));
        return services;
    }
}
