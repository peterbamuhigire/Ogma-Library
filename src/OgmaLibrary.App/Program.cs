using Avalonia;

namespace OgmaLibrary.App;

/// <summary>The process entry point and the Avalonia application builder.</summary>
public static class Program
{
    /// <summary>
    /// The application entry point. Initialization code before
    /// <see cref="BuildAvaloniaApp"/> must not use any Avalonia, third-party, or
    /// SynchronizationContext-reliant API — they are not yet initialized.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the configured Avalonia application. Used by the runtime and tooling.</summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
