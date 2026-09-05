using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OgmaLibrary.Infrastructure.Localization;
using SkiaSharp;
using Xunit;
using OgmaApp = OgmaLibrary.App.App;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for the shared Phase 18 token and control layer.</summary>
public sealed class Phase18DesignSystemTests
{
    private static string ArtifactsDir
    {
        get
        {
            string dir = Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "artifacts",
                "screenshots");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }
    }

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

    [Fact]
    public void ClassroomSearchAndReadingSummaries_HaveLocalizedResources()
    {
        var localization = new InMemoryLocalizationService();
        string[] keys =
        [
            "Classroom.SmartSearch.Title",
            "Classroom.SmartSearch.QueryWatermark",
            "Classroom.SmartSearch.Preview",
            "Classroom.SmartSearch.GroundingNotice",
            "Catalogue.BookDetail.Curation.TagsWatermark",
            "Catalogue.BookDetail.Reading.RatingFormat",
            "Catalogue.BookDetail.Reading.ProgressFormat",
            "Catalogue.BookDetail.Reading.LastReadFormat",
            "Catalogue.BookDetail.Reading.AnnotationsFormat",
            "Catalogue.BookDetail.Curation.RatingAccessibilityFormat",
            "Catalogue.Shell.ToggleSidebar",
            "Startup.Migration.Preparing",
            "Startup.Migration.ProgressFormat",
            "Search.Result.Untitled",
            "Search.Result.Separator",
            "Catalogue.BookDetail.Field.ValueMissing",
            "Catalogue.BookDetail.Field.Format",
            "Catalogue.BookDetail.Field.WithProvenanceFormat",
            "Catalogue.BookDetail.Provenance.Catalogue",
            "Catalogue.BookDetail.Provenance.Manual",
        ];

        foreach (string key in keys)
        {
            Assert.DoesNotContain("⟦", localization[key], StringComparison.Ordinal);
        }

        localization.SetCulture("fr");
        Assert.Equal("Recherche IA intelligente", localization["Classroom.SmartSearch.Title"]);
        Assert.Equal("etiquette, autre etiquette", localization["Catalogue.BookDetail.Curation.TagsWatermark"]);
        Assert.Equal("Sans titre", localization["Search.Result.Untitled"]);
        Assert.Equal(
            "Definir la note sur {0} sur 5",
            localization["Catalogue.BookDetail.Curation.RatingAccessibilityFormat"]);
        Assert.Equal("Afficher ou masquer la barre laterale", localization["Catalogue.Shell.ToggleSidebar"]);

        localization.SetCulture("qps-ploc");
        foreach (string key in keys)
        {
            Assert.StartsWith("[!!", localization[key], StringComparison.Ordinal);
        }
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

    [AvaloniaFact]
    public void AccentPalette_WhiteActionLabelsMeetSmallTextContrastInBothThemes()
    {
        var app = new OgmaApp();
        app.Initialize();

        string[] accentKeys =
        [
            "Color.Accent.Oak",
            "Color.Accent.Ink",
            "Color.Accent.Sage",
            "Color.Accent.Clay",
            "Color.Accent.Plum",
            "Color.Accent.Slate",
        ];

        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            foreach (string key in accentKeys)
            {
                Assert.True(app.TryGetResource(key, theme, out object? raw), $"Missing resource {key} for {theme}.");
                Color background = Assert.IsType<Color>(raw);
                Assert.True(
                    ContrastRatio(Colors.White, background) >= 4.5,
                    $"White action label on {key} in {theme} has contrast {ContrastRatio(Colors.White, background):F2}:1.");
            }
        }
    }

    [AvaloniaFact]
    public void RenderedAccentSurface_MeetsSmallTextContrastInBothThemes()
    {
        var app = new OgmaApp();
        app.Initialize();

        var window = new Window
        {
            Width = 320,
            Height = 180,
        };

        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            app.RequestedThemeVariant = theme;
            Assert.True(
                app.TryGetResource("Color.Accent.Oak", theme, out object? accentRaw),
                $"Missing Oak accent for {theme}.");
            Assert.True(
                app.TryGetResource("Color.Surface.Parchment", theme, out object? surfaceRaw),
                $"Missing parchment surface for {theme}.");

            Color accent = Assert.IsType<Color>(accentRaw);
            Color surface = Assert.IsType<Color>(surfaceRaw);
            window.Content = new Grid
            {
                Background = new SolidColorBrush(surface),
                Children =
                {
                    new Border
                    {
                        Width = 220,
                        Height = 60,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Background = new SolidColorBrush(accent),
                        Child = new TextBlock
                        {
                            Text = "Open book",
                            Foreground = new SolidColorBrush(Colors.White),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        },
                    },
                },
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame!.Save(Path.Combine(
                ArtifactsDir,
                $"phase18-contrast-{(theme == ThemeVariant.Light ? "light" : "dark")}.png"));

            using var stream = new MemoryStream();
            frame.Save(stream);
            stream.Position = 0;
            using SKBitmap bitmap = SKBitmap.Decode(stream)
                ?? throw new InvalidOperationException("Headless frame could not be decoded.");

            SKColor renderedAccent = bitmap.GetPixel(bitmap.Width / 2 - 90, bitmap.Height / 2);
            SKColor renderedSurface = bitmap.GetPixel(8, 8);
            Assert.True(
                ColorDistance(renderedAccent, accent) <= 3,
                $"Rendered Oak accent drifted for {theme}: {renderedAccent} vs {accent}.");
            Assert.True(
                ColorDistance(renderedSurface, surface) <= 3,
                $"Rendered parchment drifted for {theme}: {renderedSurface} vs {surface}.");
            Assert.True(
                ContrastRatio(
                    new Color(renderedAccent.Alpha, renderedAccent.Red, renderedAccent.Green, renderedAccent.Blue),
                    Colors.White) >= 4.5,
                $"Rendered white action label on Oak fails WCAG AA for {theme}.");
        }

        window.Close();
    }

    private static int ColorDistance(SKColor actual, Color expected) =>
        Math.Abs(actual.Red - expected.R)
        + Math.Abs(actual.Green - expected.G)
        + Math.Abs(actual.Blue - expected.B);

    private static double ContrastRatio(Color foreground, Color background)
    {
        static double RelativeLuminance(Color color)
        {
            static double LinearChannel(byte channel)
            {
                double value = channel / 255d;
                return value <= 0.03928
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * LinearChannel(color.R))
                + (0.7152 * LinearChannel(color.G))
                + (0.0722 * LinearChannel(color.B));
        }

        double foregroundLuminance = RelativeLuminance(foreground);
        double backgroundLuminance = RelativeLuminance(background);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }
}
