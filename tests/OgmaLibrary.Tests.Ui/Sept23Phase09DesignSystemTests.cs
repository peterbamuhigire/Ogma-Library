using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OgmaLibrary.App.Icons;
using OgmaLibrary.App.Views.Catalogue;
using Xunit;
using OgmaApp = OgmaLibrary.App.App;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 09 (K15): the "Lamplit Library" token system is enforced. Contrast matrix over
/// every semantic role pair in Light and Dark, the type-scale floor and ratio, the static guard
/// against hard-coded styling in views, the icon register, generated covers and variant styles.
/// </summary>
public sealed partial class Sept23Phase09DesignSystemTests
{
    private static readonly ThemeVariant[] Themes = [ThemeVariant.Light, ThemeVariant.Dark];

    /// <summary>Text pairs that must reach 4.5:1 (body text) in both themes.</summary>
    public static TheoryData<string, string> BodyTextPairs => new()
    {
        { "Color.Text.Primary", "Color.Surface.Base" },
        { "Color.Text.Primary", "Color.Surface.Raised" },
        { "Color.Text.Primary", "Color.Surface.Sunken" },
        { "Color.Text.Secondary", "Color.Surface.Base" },
        { "Color.Text.Secondary", "Color.Surface.Raised" },
        { "Color.Text.Secondary", "Color.Surface.Sunken" },
        { "Color.Text.Primary", "Color.Control.Background" },
        { "Color.Text.Primary", "Color.Control.BackgroundHover" },
        { "Color.Text.Primary", "Color.Control.BackgroundPressed" },
        { "Color.Accent.Text", "Color.Surface.Base" },
        { "Color.Accent.Text", "Color.Surface.Raised" },
        { "Color.Accent.Text", "Color.Selection.Background" },
        { "Color.Link", "Color.Surface.Base" },
        { "Color.Link", "Color.Surface.Raised" },
        { "Color.Selection.Foreground", "Color.Selection.Background" },
        { "Color.Selection.Foreground", "Color.Selection.BackgroundHover" },
        { "Color.Rail.Foreground", "Color.Rail.Background" },
        { "Color.Rail.ForegroundMuted", "Color.Rail.Background" },
        { "Color.Rail.Foreground", "Color.Rail.ItemHover" },
        { "Color.Rail.Foreground", "Color.Rail.ItemSelected" },
        { "Color.Accent.OnAccent", "Color.Accent.Primary" },
        { "Color.Accent.OnAccent", "Color.Accent.PrimaryHover" },
        { "Color.Accent.OnAccent", "Color.Accent.PrimaryPressed" },
        { "Color.Accent.OnAccent", "Color.Accent.Oak" },
        { "Color.Accent.OnAccent", "Color.Accent.Ink" },
        { "Color.Accent.OnAccent", "Color.Accent.Sage" },
        { "Color.Accent.OnAccent", "Color.Accent.Clay" },
        { "Color.Accent.OnAccent", "Color.Accent.Plum" },
        { "Color.Accent.OnAccent", "Color.Accent.Slate" },
        { "Color.Accent.OnAccent", "Color.Accent.Saffron" },
        { "Color.Accent.OnAccent", "Color.Accent.Teal" },
        { "Color.Accent.OnAccent", "Color.Status.Success" },
        { "Color.Accent.OnAccent", "Color.Status.Warning" },
        { "Color.Accent.OnAccent", "Color.Status.Danger" },
        { "Color.Accent.OnAccent", "Color.Status.DangerHover" },
        { "Color.Accent.OnAccent", "Color.Status.DangerPressed" },
        { "Color.Accent.OnAccent", "Color.Status.Info" },
        { "Color.Accent.OnAccent", "Color.Status.Neutral" },
        { "Color.Cover.Gilt", "Color.Accent.Oak" },
        { "Color.Cover.Gilt", "Color.Accent.Ink" },
        { "Color.Cover.Gilt", "Color.Accent.Sage" },
        { "Color.Cover.Gilt", "Color.Accent.Clay" },
        { "Color.Cover.Gilt", "Color.Accent.Plum" },
        { "Color.Cover.Gilt", "Color.Accent.Slate" },
        { "Color.Cover.Gilt", "Color.Accent.Saffron" },
        { "Color.Cover.Gilt", "Color.Accent.Teal" },
    };

    /// <summary>Non-text pairs (focus rings, boundaries, icons, disabled labels) that must reach 3:1.</summary>
    public static TheoryData<string, string> NonTextPairs => new()
    {
        { "Color.Focus", "Color.Surface.Base" },
        { "Color.Focus", "Color.Surface.Raised" },
        { "Color.Focus", "Color.Selection.Background" },
        { "Color.Rail.Focus", "Color.Rail.Background" },
        { "Color.Rail.Focus", "Color.Rail.ItemSelected" },
        { "Color.Control.Border", "Color.Surface.Base" },
        { "Color.Control.Border", "Color.Surface.Raised" },
        { "Color.Selection.Border", "Color.Surface.Raised" },
        { "Color.Text.Disabled", "Color.Control.BackgroundDisabled" },
        { "Color.Hue.Amber", "Color.Surface.Base" },
        { "Color.Hue.Lapis", "Color.Surface.Base" },
        { "Color.Hue.Malachite", "Color.Surface.Base" },
        { "Color.Hue.Plum", "Color.Surface.Base" },
        { "Color.Hue.Saffron", "Color.Surface.Base" },
        { "Color.Hue.Garnet", "Color.Surface.Base" },
        { "Color.Hue.Teal", "Color.Surface.Base" },
        { "Color.Hue.Slate", "Color.Surface.Base" },
        { "Color.Hue.Amber", "Color.Rail.Background" },
        { "Color.Hue.Lapis", "Color.Rail.Background" },
        { "Color.Hue.Malachite", "Color.Rail.Background" },
        { "Color.Hue.Plum", "Color.Rail.Background" },
        { "Color.Hue.Saffron", "Color.Rail.Background" },
        { "Color.Hue.Garnet", "Color.Rail.Background" },
        { "Color.Hue.Teal", "Color.Rail.Background" },
        { "Color.Hue.Slate", "Color.Rail.Background" },
        { "Color.Hue.Amber", "Color.Surface.Raised" },
        { "Color.Hue.Lapis", "Color.Surface.Raised" },
        { "Color.Hue.Malachite", "Color.Surface.Raised" },
        { "Color.Hue.Plum", "Color.Surface.Raised" },
        { "Color.Hue.Saffron", "Color.Surface.Raised" },
        { "Color.Hue.Garnet", "Color.Surface.Raised" },
        { "Color.Hue.Teal", "Color.Surface.Raised" },
        { "Color.Hue.Slate", "Color.Surface.Raised" },
    };

    [AvaloniaTheory]
    [MemberData(nameof(BodyTextPairs))]
    public void ContrastMatrix_TextPairsMeetAaInBothThemes(string foreground, string background)
    {
        OgmaApp app = NewApp();
        foreach (ThemeVariant theme in Themes)
        {
            double ratio = Contrast(Resolve(app, foreground, theme), Resolve(app, background, theme));
            Assert.True(ratio >= 4.5, $"{foreground} on {background} in {theme}: {ratio:F2}:1 < 4.5:1");
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(NonTextPairs))]
    public void ContrastMatrix_NonTextPairsMeetThreeToOneInBothThemes(string foreground, string background)
    {
        OgmaApp app = NewApp();
        foreach (ThemeVariant theme in Themes)
        {
            double ratio = Contrast(Resolve(app, foreground, theme), Resolve(app, background, theme));
            Assert.True(ratio >= 3.0, $"{foreground} on {background} in {theme}: {ratio:F2}:1 < 3:1");
        }
    }

    [AvaloniaFact]
    public void DarkTheme_IsNightLibraryNotAGreyInversion()
    {
        OgmaApp app = NewApp();
        Color canvas = Resolve(app, "Color.Surface.Base", ThemeVariant.Dark);

        // Deep indigo: blue channel clearly dominant, and dark.
        Assert.True(canvas.B > canvas.R + 20 && canvas.B > canvas.G + 10, $"Night canvas {canvas} is not indigo.");
        Assert.True(Luminance(canvas) < 0.03, $"Night canvas {canvas} is not dark.");
        Assert.NotEqual(
            Resolve(app, "Color.Surface.Base", ThemeVariant.Light),
            Resolve(app, "Color.Surface.Base", ThemeVariant.Dark));
    }

    [AvaloniaFact]
    public void TypeScale_HasTwelvePixelFloorAndRealRatio()
    {
        OgmaApp app = NewApp();
        string[] all =
        [
            "Type.Size.Caption", "Type.Size.Small", "Type.Size.Body", "Type.Size.BodyLarge",
            "Type.Size.BodyMedium", "Type.Size.Subtitle", "Type.Size.Title", "Type.Size.Headline",
            "Type.Size.Display", "Type.Size.Glyph",
        ];
        foreach (string key in all)
        {
            Assert.True(Size(app, key) >= 12, $"{key} is below the 12 px floor.");
        }

        // Hierarchy steps: >= 1.2 in the UI range and >= 1.25 at the display end.
        Assert.True(Size(app, "Type.Size.Subtitle") / Size(app, "Type.Size.Body") >= 1.2);
        Assert.True(Size(app, "Type.Size.Title") / Size(app, "Type.Size.Subtitle") >= 1.2);
        Assert.True(Size(app, "Type.Size.Headline") / Size(app, "Type.Size.Title") >= 1.25);
        Assert.True(Size(app, "Type.Size.Display") / Size(app, "Type.Size.Headline") >= 1.25);
    }

    [Fact]
    public void Views_HaveNoHardCodedFontSizesOrColours()
    {
        string views = Path.Combine(RepoRoot(), "src", "OgmaLibrary.App", "Views");
        var violations = new List<string>();
        foreach (string file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (LiteralFontSize().IsMatch(lines[i]) || LiteralColour().IsMatch(lines[i]))
                {
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0, "Hard-coded styling in views:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void IconSet_EveryAssetIsRegisteredDuotoneAndLicensed()
    {
        string root = RepoRoot();
        string icons = Path.Combine(root, "src", "OgmaLibrary.App", "Assets", "icons");
        Assert.True(File.Exists(Path.Combine(icons, "LICENSE-ICONS.txt")));
        string register = File.ReadAllText(Path.Combine(root, "docs", "governance", "asset-licence-register.md"));
        Assert.Contains("@iconify-json/lucide", register, StringComparison.Ordinal);
        Assert.Contains("ISC", register, StringComparison.Ordinal);

        string[] svgs = Directory.GetFiles(icons, "*.svg", SearchOption.AllDirectories);
        Assert.NotEmpty(svgs);
        foreach (string svg in svgs)
        {
            string relative = Path.GetRelativePath(icons, svg).Replace('\\', '/');
            string key = Path.GetFileNameWithoutExtension(svg);
            Assert.Equal(
                "avares://OgmaLibrary.App/Assets/icons/" + relative,
                IconCatalog.GetAvaresPath(key));
            string content = File.ReadAllText(svg);
            Assert.Contains("fill-opacity=\"0.2\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("<image", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GeneratedCover_IsDeterministicAndVaried()
    {
        Assert.Equal(CoverDesign.For("Middlemarch"), CoverDesign.For("Middlemarch"));
        Assert.Equal(CoverDesign.For("Middlemarch"), CoverDesign.For("  middlemarch "));
        Assert.Equal(2166136261u, CoverDesign.StableHash(string.Empty));

        var titles = Enumerable.Range(1, 17).Select(i => $"Synthetic Book {i}").ToList();
        int distinct = titles.Select(t => CoverDesign.For(t).ClothKey).Distinct().Count();
        Assert.True(distinct >= 5, $"17 titles produced only {distinct} cloth colours.");
        Assert.Equal("MM", GeneratedCover.Monogram("The Middle March"));
        Assert.Equal("?", GeneratedCover.Monogram(null));
    }

    [AvaloniaFact]
    public void GeneratedCover_RendersClothAndGiltInBothThemes()
    {
        foreach (ThemeVariant theme in Themes)
        {
            var cover = new GeneratedCover { Title = "The Lamplit Library", Author = "A. Reader", Width = 160, Height = 220 };
            var window = new Window { Width = 200, Height = 260, RequestedThemeVariant = theme, Content = cover };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame!.Save(Path.Combine(ArtifactsDir(), $"phase09-cover-{theme}.png"));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ButtonVariants_ApplyTokenFillsAndFocusRing()
    {
        OgmaApp app = NewApp();
        var primary = new Button { Content = "Open", Classes = { "primary" } };
        var danger = new Button { Content = "Remove", Classes = { "danger" } };
        var secondary = new Button { Content = "Cancel" };
        var tool = new Button { Content = "Filter", Classes = { "tool" } };
        var link = new Button { Content = "Details", Classes = { "link" } };
        var window = new Window
        {
            Width = 600,
            Height = 120,
            Content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { primary, danger, secondary, tool, link } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Resolve(app, "Color.Accent.Primary", ThemeVariant.Light), ((ISolidColorBrush)primary.Background!).Color);
        Assert.Equal(Resolve(app, "Color.Accent.OnAccent", ThemeVariant.Light), ((ISolidColorBrush)primary.Foreground!).Color);
        Assert.Equal(Resolve(app, "Color.Status.Danger", ThemeVariant.Light), ((ISolidColorBrush)danger.Background!).Color);
        Assert.Equal(Resolve(app, "Color.Control.Background", ThemeVariant.Light), ((ISolidColorBrush)secondary.Background!).Color);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)tool.Background!).Color);
        Assert.Equal(Resolve(app, "Color.Link", ThemeVariant.Light), ((ISolidColorBrush)link.Foreground!).Color);

        secondary.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Resolve(app, "Color.Focus", ThemeVariant.Light), ((ISolidColorBrush)secondary.BorderBrush!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void TabItems_UseBodySizeNotDisplaySize()
    {
        NewApp();
        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "File" }, new TabItem { Header = "Bibliographic" },
                new TabItem { Header = "Reading" }, new TabItem { Header = "Enrichment" },
            },
        };
        var window = new Window { Width = 360, Height = 200, Content = tabs };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        foreach (TabItem item in tabs.ItemsSource!.Cast<TabItem>())
        {
            Assert.Equal(14, item.FontSize);
        }

        // One row: every header shares the first header's top edge (no wrapping).
        double top = tabs.ItemsSource!.Cast<TabItem>().First().Bounds.Top;
        Assert.All(tabs.ItemsSource!.Cast<TabItem>(), item => Assert.Equal(top, item.Bounds.Top));
        window.Close();
    }

    [AvaloniaFact]
    public void ComponentGallery_RendersEveryStateInBothThemesWithLocalizedLabels()
    {
        NewApp();
        foreach (string culture in new[] { "en", "fr" })
        {
            var localization = new OgmaLibrary.Infrastructure.Localization.InMemoryLocalizationService();
            localization.SetCulture(culture);
            var model = new OgmaLibrary.App.Views.Dev.ComponentGalleryModel(localization);
            foreach (var property in typeof(OgmaLibrary.App.Views.Dev.ComponentGalleryModel).GetProperties()
                         .Where(p => p.PropertyType == typeof(string)))
            {
                string value = (string)property.GetValue(model)!;
                Assert.False(value.StartsWith('⟦'), $"Missing {culture} label for {property.Name}.");
            }
        }

        foreach (ThemeVariant theme in Themes)
        {
            var localization = new OgmaLibrary.Infrastructure.Localization.InMemoryLocalizationService();
            var gallery = new OgmaLibrary.App.Views.Dev.ComponentGallery
            {
                DataContext = new OgmaLibrary.App.Views.Dev.ComponentGalleryModel(localization),
            };
            var window = new Window { Width = 1180, Height = 1500, RequestedThemeVariant = theme, Content = gallery };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame!.Save(Path.Combine(ArtifactsDir(), $"phase09-gallery-{theme}.png"));
            window.Close();
        }
    }

    private static OgmaApp NewApp()
    {
        var app = new OgmaApp();
        app.Initialize();
        return app;
    }

    private static Color Resolve(OgmaApp app, string key, ThemeVariant theme)
    {
        Assert.True(app.TryGetResource(key, theme, out object? raw), $"Missing {key} for {theme}.");
        return Assert.IsType<Color>(raw);
    }

    private static double Size(OgmaApp app, string key)
    {
        Assert.True(app.TryGetResource(key, ThemeVariant.Light, out object? raw), $"Missing {key}.");
        return Assert.IsType<double>(raw);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte c)
        {
            double v = c / 255d;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static double Contrast(Color a, Color b)
    {
        // Translucent foregrounds are not allowed in the matrix; composite rules would hide failures.
        Assert.Equal(255, a.A);
        Assert.Equal(255, b.A);
        double la = Luminance(a);
        double lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string ArtifactsDir()
    {
        string dir = Path.Combine(RepoRoot(), "artifacts", "screenshots");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [GeneratedRegex(@"FontSize=""\s*[0-9.]+\s*""|Property=""FontSize""\s+Value=""\s*[0-9.]+")]
    private static partial Regex LiteralFontSize();

    [GeneratedRegex(@"(?:Foreground|Background|BorderBrush|Fill|Stroke|Color)=""\s*(?:#[0-9A-Fa-f]{3,8}|(?!Transparent\b)[A-Z][a-zA-Z]+)\s*""")]
    private static partial Regex LiteralColour();
}
