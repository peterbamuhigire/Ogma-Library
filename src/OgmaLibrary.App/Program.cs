using Avalonia;
using Microsoft.Extensions.Logging;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.App;

/// <summary>The process entry point and the Avalonia application builder.</summary>
public static class Program
{
    /// <summary>The single-instance guard owned by this (primary) process, if any.</summary>
    public static SingleInstanceGuard? InstanceGuard { get; private set; }

    /// <summary>
    /// The application entry point. Initialization code before
    /// <see cref="BuildAvaloniaApp"/> must not use any Avalonia, third-party, or
    /// SynchronizationContext-reliant API — they are not yet initialized.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        string dataDirectory = ResolveDataDirectory();

        // K70: a second launch hands off to the running instance and exits before it touches
        // the catalogue database, the workers or the LAN listener.
        using SingleInstanceGuard instance = SingleInstanceGuard.Acquire(dataDirectory);
        if (!instance.IsPrimary)
        {
            bool delivered = instance.TrySignalPrimary(FirstPdfArgument(args), TimeSpan.FromMilliseconds(1500));
            System.Diagnostics.Trace.WriteLine($"app.instance.secondary: activation delivered={delivered}");
            return 0;
        }

        InstanceGuard = instance;
        AppDiagnostics.Initialize(dataDirectory, ResolveLibraryRoots());
        GlobalExceptionHandlers.InstallProcessHandlers();
        ILogger logger = AppDiagnostics.CreateLogger<App>();
        string guardMode = UiThreadGuard.ResolveMode().ToString();
        AppLog.AppStarted(logger, AppDiagnostics.AppVersion, guardMode);
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppLog.AppExited(logger);
            InstanceGuard = null;
            AppDiagnostics.Shutdown();
        }
    }

    /// <summary>Builds the configured Avalonia application. Used by the runtime and tooling.</summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static string ResolveDataDirectory()
    {
        try
        {
            return CatalogueServiceExtensions.GetDefaultDataDirectory();
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unusable OGMA_LIBRARY_DATA_DIR is reported by composition; guard the default folder.
            System.Diagnostics.Trace.WriteLine("ogma data directory unusable: " + exception.GetType().Name);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ogma Library Data");
        }
    }

    private static string[] ResolveLibraryRoots()
    {
        string? root = Environment.GetEnvironmentVariable("OGMA_LIBRARY_ROOT");
        return string.IsNullOrWhiteSpace(root) ? [] : [root];
    }

    private static string? FirstPdfArgument(string[] args) =>
        args.FirstOrDefault(argument => argument.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
}
