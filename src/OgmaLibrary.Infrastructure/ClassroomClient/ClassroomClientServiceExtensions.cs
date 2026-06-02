using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Registers the Phase 17 Classroom Client bounded context without activating Client mode.</summary>
public static class ClassroomClientServiceExtensions
{
    /// <summary>Adds inactive Client/Classroom mode services.</summary>
    public static IServiceCollection AddClassroomClientServices(
        this IServiceCollection services,
        string dataDirectory,
        IClassroomCredentialStore? credentialStore = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddSingleton<IClassroomModeService>(_ => new FileClassroomModeService(dataDirectory));
        services.AddSingleton<IClassroomHostConnectionService, InMemoryClassroomHostConnectionService>();
        services.AddSingleton<IClassroomCredentialStore>(_ => credentialStore ??
            new PlatformClassroomCredentialStore(ClassroomSecretStoreFactory.Create(dataDirectory)));
        services.AddSingleton<IClassroomSyncBlobCodec, ClassroomSyncBlobCodec>();
        services.AddSingleton<IProfileService>(provider => new FileClassroomProfileService(
            dataDirectory,
            provider.GetRequiredService<IStudentPrivateRepository>(),
            provider.GetRequiredService<IClassroomCredentialStore>()));
        services.AddSingleton<IOfflineCacheService>(_ => new DiskOfflineCacheService(dataDirectory));
        services.AddSingleton<IStudentPrivateRepository>(_ => new StudentPrivateRepository(dataDirectory));
        services.AddSingleton<IHostCertificateFingerprintProbe, TlsHostCertificateFingerprintProbe>();
        services.AddSingleton<LibraryHostHttpClient>(provider => new LibraryHostHttpClient(
            new HttpClient(),
            provider.GetRequiredService<IHostCertificateFingerprintProbe>()));
        services.AddSingleton<ILibraryHostClient>(provider => new CachingLibraryHostClient(
            provider.GetRequiredService<LibraryHostHttpClient>(),
            provider.GetRequiredService<IOfflineCacheService>()));
        services.AddSingleton<ISyncService, ClassroomSyncService>();
        services.AddSingleton<IClassroomBookFileMaterializer>(provider => new ClassroomBookFileMaterializer(
            dataDirectory,
            provider.GetRequiredService<ILibraryHostClient>()));
        services.AddSingleton<IClassroomJoinParser, ClassroomJoinParser>();
        services.AddSingleton<IMdnsResolver, MdnsResolver>();
        services.AddSingleton<IHostTrustStore>(provider => new CredentialBackedHostTrustStore(
            provider.GetRequiredService<IClassroomCredentialStore>()));
        services.AddSingleton<IHostTrustService, HostTrustService>();
        services.AddSingleton<IClassroomConnectionService, ClassroomConnectionService>();
        return services;
    }
}
