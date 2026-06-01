using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;

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
        return services;
    }
}
