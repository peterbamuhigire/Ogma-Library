using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using OgmaApp = OgmaLibrary.App.App;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 09 slice 4: guards for defects found in the real-window visual pass. Each test
/// pins a surface to the Lamplit tokens where the Fluent default leaked through.
/// </summary>
public sealed class Sept23Phase09RealWindowFixTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void TextBox_FocusedField_UsesRaisedSurfaceNotFluentBlackOrWhite(string variant)
    {
        ThemeVariant theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        OgmaApp app = NewApp();
        var field = new TextBox { Text = "Wanjiru"};
        var window = new Window { Width = 400, Height = 120, RequestedThemeVariant = theme, Content = field };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        field.Focus();
        Dispatcher.UIThread.RunJobs();

        Border border = field.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
        Assert.True(app.TryGetResource("Color.Surface.Raised", theme, out object? raw));
        Assert.Equal(Assert.IsType<Color>(raw), Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
        window.Close();
    }

    private static OgmaApp NewApp()
    {
        var app = new OgmaApp();
        app.Initialize();
        return app;
    }
}
