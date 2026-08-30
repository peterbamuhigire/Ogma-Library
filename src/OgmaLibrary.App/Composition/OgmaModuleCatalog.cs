using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Configuration;

namespace OgmaLibrary.App.Composition;

internal interface IOgmaModuleRegistrar
{
    string Name { get; }

    void Register(IServiceCollection services, OgmaRuntimeOptions options);
}

internal static class OgmaModuleCatalog
{
    public static IReadOnlyList<IOgmaModuleRegistrar> Modules { get; } =
    [
        new CorePlatformModule(),
        new CatalogueProcessingModule(),
        new ClassroomModule(),
        new ReaderModule(),
        new ShellModule(),
        new StartupModule(),
    ];
}
