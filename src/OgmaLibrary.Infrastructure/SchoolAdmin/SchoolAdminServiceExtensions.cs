using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>Registers the Phase 36 School Administration bounded context.</summary>
public static class SchoolAdminServiceExtensions
{
    /// <summary>Adds Host-admin services; capability activation remains policy and role controlled.</summary>
    public static IServiceCollection AddSchoolAdminServices(this IServiceCollection services, string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(Microsoft.EntityFrameworkCore.IDbContextFactory<CatalogueDbContext>)))
        {
            services.AddCatalogueContext(dataDirectory, dataDirectory);
        }

        services.AddSingleton<SchoolAdminCatalogueService>();
        services.AddSingleton<SchoolProfileEnrollmentService>();
        services.AddSingleton<SchoolAiPolicyService>();
        services.AddSingleton<SchoolUsageDashboardService>();
        services.AddSingleton<SchoolAiHistoryManagementService>();
        services.AddSingleton(_ => new SchoolBackupService(dataDirectory));
        services.AddSingleton<SchoolDpiaScreeningService>();
        services.AddSingleton<UnavailableSchoolAdminService>();
        services.AddSingleton<ILibraryPublishingService>(provider =>
            provider.GetRequiredService<SchoolAdminCatalogueService>());
        services.AddSingleton<ISharedShelfService>(provider =>
            provider.GetRequiredService<SchoolAdminCatalogueService>());
        services.AddSingleton<IProfileEnrollmentService>(provider =>
            provider.GetRequiredService<SchoolProfileEnrollmentService>());
        services.AddSingleton<ISchoolAiPolicyService>(provider =>
            provider.GetRequiredService<SchoolAiPolicyService>());
        services.AddSingleton<ISchoolAiKeyProvider>(provider =>
        {
            IClassroomCredentialStore? credentialStore = provider.GetService<IClassroomCredentialStore>();
            return credentialStore is null
                ? provider.GetRequiredService<UnavailableSchoolAdminService>()
                : new SchoolAiKeyProvider(credentialStore);
        });
        services.AddSingleton<IAiProxyEndpointHandler>(provider =>
            new AiProxyEndpointHandler(
                provider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<CatalogueDbContext>>(),
                provider.GetRequiredService<OgmaLibrary.Application.Catalogue.ICatalogueReadModel>(),
                provider.GetRequiredService<ISchoolAiPolicyService>(),
                provider.GetRequiredService<IDpiaScreeningService>(),
                provider.GetService<IAiProvider>(),
                provider.GetService<IAiCostCalculator>()));
        services.AddSingleton<IUsageDashboardService>(provider =>
            provider.GetRequiredService<SchoolUsageDashboardService>());
        services.AddSingleton<ISchoolAiHistoryManagementService>(provider =>
            provider.GetRequiredService<SchoolAiHistoryManagementService>());
        services.AddSingleton<ISchoolBackupService>(provider =>
            provider.GetRequiredService<SchoolBackupService>());
        services.AddSingleton<IDpiaScreeningService>(provider =>
            provider.GetRequiredService<SchoolDpiaScreeningService>());
        return services;
    }
}
