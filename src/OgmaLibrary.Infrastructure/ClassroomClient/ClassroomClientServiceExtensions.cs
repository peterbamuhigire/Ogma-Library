using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Registers the Phase 17 Classroom Client bounded context without activating Client mode.</summary>
public static class ClassroomClientServiceExtensions
{
    /// <summary>Adds inactive Client/Classroom mode services.</summary>
    public static IServiceCollection AddClassroomClientServices(
        this IServiceCollection services,
        string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddSingleton<IClassroomModeService, InMemoryClassroomModeService>();
        services.AddSingleton<IProfileService, InMemoryProfileService>();
        services.AddSingleton<ISyncService, UnavailableSyncService>();
        services.AddSingleton<IOfflineCacheService, InMemoryOfflineCacheService>();
        services.AddSingleton<IStudentPrivateRepository>(_ => new StudentPrivateRepository(dataDirectory));
        services.AddSingleton<ILibraryHostClient, UnavailableLibraryHostClient>();
        services.AddSingleton<IClassroomJoinParser, ClassroomJoinParser>();
        services.AddSingleton<IMdnsResolver, MdnsResolver>();
        services.AddSingleton<IHostTrustStore, InMemoryHostTrustStore>();
        services.AddSingleton<IHostTrustService, HostTrustService>();
        return services;
    }
}
