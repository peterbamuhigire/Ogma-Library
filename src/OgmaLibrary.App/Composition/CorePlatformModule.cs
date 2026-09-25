using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Security;

namespace OgmaLibrary.App.Composition;

internal sealed class CorePlatformModule : IOgmaModuleRegistrar
{
    public string Name => "core-platform";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        // Sept-23 Phase 02: the desktop App binds the rolling file sink, the toast surface and
        // the Avalonia dispatcher before this module runs. Headless composition (tests, tools)
        // keeps working with the null/inline fallbacks below.
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.TryAddSingleton<IUserNotifier>(NullUserNotifier.Instance);
        services.TryAddSingleton<IUiDispatcher>(InlineUiDispatcher.Instance);
        services.AddSingleton<IBenchmarkContext, StopwatchBenchmarkContext>();
        services.AddSingleton<ILocalizationService, InMemoryLocalizationService>();
        services.AddSingleton<IUserPreferencesService>(_ =>
            new FileUserPreferencesService(options.DataDirectory));
        services.AddSingleton<IWebViewBridge>(_ =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? new WKWebViewBridge()
                : new WebView2Bridge());
        services.AddSingleton<IShelf3DHostCoordinator, Shelf3DHostCoordinator>();
        services.AddSingleton<IPasswordProvider>(_ =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new WindowsPasswordProvider()
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? new MacOsKeychainPasswordProvider()
                    : new UnsupportedPasswordProvider());
    }
}
