using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.Application;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Security;

namespace OgmaLibrary.App.Composition;

internal sealed class CorePlatformModule : IOgmaModuleRegistrar
{
    public string Name => "core-platform";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        services.AddSingleton<IBenchmarkContext, StopwatchBenchmarkContext>();
        services.AddSingleton<ILocalizationService, InMemoryLocalizationService>();
        services.AddSingleton<IWebViewBridge>(_ =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? new WKWebViewBridge()
                : new WebView2Bridge());
        services.AddSingleton<IPasswordProvider>(_ =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new WindowsPasswordProvider()
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? new MacOsKeychainPasswordProvider()
                    : new UnsupportedPasswordProvider());
    }
}
