using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.Infrastructure.Localization;
using OgmaApp = OgmaLibrary.App.App;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for the shared Phase 18 token and control layer.</summary>
public sealed class Phase18DesignSystemTests
{
    [Fact]
    public void DetailPanelLabels_HaveEnglishFrenchAndPseudoResources()
    {
        var localization = new InMemoryLocalizationService();

        Assert.Equal("File", localization["Catalogue.BookDetail.Tab.File"]);
        localization.SetCulture("fr");
        Assert.Equal("Fichier", localization["Catalogue.BookDetail.Tab.File"]);
        localization.SetCulture("qps-ploc");
        Assert.Contains("[!!", localization["Catalogue.BookDetail.Tab.File"], StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void App_ProvidesFontRolesFocusTokenAndAccessibleControlTarget()
    {
        var app = new OgmaApp();
        app.Initialize();

        Assert.NotNull(app.FindResource("Type.FontFamily.Body"));
        Assert.NotNull(app.FindResource("Type.FontFamily.Display"));
        Assert.NotNull(app.FindResource("Type.FontFamily.Mono"));
        Assert.True(app.TryGetResource("Brush.Focus", app.RequestedThemeVariant, out object? focus));
        Assert.NotNull(focus);

        var button = new Button { Content = "Action" };
        var window = new Window { Width = 320, Height = 180, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.MinHeight >= 36);
        Assert.True(button.Bounds.Height >= 36);
        window.Close();
    }
}
