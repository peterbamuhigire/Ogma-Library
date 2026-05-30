using Avalonia;
using Avalonia.Headless;
using OgmaLibrary.Tests.Ui;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Configures the headless Avalonia application used by the UI tests. Headless drawing
/// is disabled and Skia is used so that <c>CaptureRenderedFrame</c> produces a real
/// rendered image (used for the skeleton screenshot and visual smoke checks).
/// </summary>
public static class TestAppBuilder
{
    /// <summary>Builds the headless, Skia-backed Avalonia app for tests.</summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<OgmaLibrary.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
