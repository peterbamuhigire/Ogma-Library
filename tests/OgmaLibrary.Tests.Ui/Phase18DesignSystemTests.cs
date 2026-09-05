using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using OgmaApp = OgmaLibrary.App.App;

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
