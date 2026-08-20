using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.ClassroomClient;
using OgmaLibrary.Infrastructure.LanHost;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.App.Composition;

internal sealed class ClassroomModule : IOgmaModuleRegistrar
{
    public string Name => "classroom";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        services.AddLanHostServices(options.DataDirectory);
        services.AddClassroomClientServices(options.DataDirectory);
        services.AddSchoolAdminServices(options.DataDirectory);

        services.AddSingleton<ClassroomCatalogueReadModel>(sp => new ClassroomCatalogueReadModel(
            sp.GetRequiredService<CatalogueReadModel>(),
            sp.GetRequiredService<IClassroomModeService>(),
            sp.GetRequiredService<IClassroomHostConnectionService>(),
            sp.GetRequiredService<ILibraryHostClient>()));
        services.AddSingleton<ICatalogueReadModel>(sp =>
            sp.GetRequiredService<ClassroomCatalogueReadModel>());
    }
}
