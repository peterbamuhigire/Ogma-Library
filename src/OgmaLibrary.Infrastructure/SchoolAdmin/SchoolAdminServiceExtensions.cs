using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>Registers the Phase 18 School Administration bounded-context scaffold.</summary>
public static class SchoolAdminServiceExtensions
{
    /// <summary>Adds disabled School Administration services until Host admin activation is implemented.</summary>
    public static IServiceCollection AddSchoolAdminServices(this IServiceCollection services, string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddSingleton<UnavailableSchoolAdminService>();
        services.AddSingleton<ILibraryPublishingService>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<ISharedShelfService>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<IProfileEnrollmentService>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<ISchoolAiPolicyService>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<ISchoolAiKeyProvider>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<IAiProxyEndpointHandler>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<IUsageDashboardService>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        services.AddSingleton<IDpiaScreeningService>(provider =>
            provider.GetRequiredService<UnavailableSchoolAdminService>());
        return services;
    }
}
