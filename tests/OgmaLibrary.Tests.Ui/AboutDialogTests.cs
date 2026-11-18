using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.About;
using OgmaLibrary.App.ViewModels.About;
using OgmaLibrary.App.Views.About;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.App.Views.Settings;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using OgmaApp = OgmaLibrary.App.App;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// The About Ogma Library dialog: exact credit data, the link allow-list, localised labels in
/// English and French, rendering in Light and Dark with token contrast, and its entry points.
/// </summary>
public sealed class AboutDialogTests
{
    private static readonly string[] AboutKeys =
    [
        "Command.App.About", "About.Title", "About.Tagline", "About.LeadDeveloper", "About.Website",
        "About.Email", "About.Phone", "About.OgmaSite", "About.Online", "About.Version.Format",
        "About.Copyright.Format", "About.Licences.Format", "About.Close", "About.Link.NameFormat",
        "About.Link.Help", "About.LinkFailed",
    ];

    private static readonly string[] LinkIds =
        ["About.Link.Website", "About.Link.Email", "About.Link.Phone", "About.Link.OgmaSite"];

    [Fact]
    public void Credits_HoldExactlyTheOwnersValues()
    {
        Assert.Equal("Ogma Library", AppCredits.ProductName);
        Assert.Equal("Chwezi Core Systems", AppCredits.CopyrightHolder);
        Assert.Equal("Peter Bamuhigire", AppCredits.LeadDeveloperName);
        Assert.Equal("www.techguypeter.com", AppCredits.WebsiteDisplay);
        Assert.Equal("https://www.techguypeter.com", AppCredits.WebsiteUrl);
        Assert.Equal("peter@techguypeter.com", AppCredits.Email);
        Assert.Equal("mailto:peter@techguypeter.com", AppCredits.EmailUrl);
        Assert.Equal("+256 784 464178", AppCredits.PhoneDisplay);
        Assert.Equal("tel:+256784464178", AppCredits.PhoneUrl);
        Assert.Equal("https://ogma.techguypeter.com/", AppCredits.OgmaSiteUrl);
        Assert.Equal(4, AppCredits.KnownLinks.Count);
    }

    [Theory]
    [InlineData("https://www.techguypeter.com", true)]
    [InlineData("https://www.techguypeter.com/", true)]
    [InlineData("mailto:peter@techguypeter.com", true)]
    [InlineData("tel:+256784464178", true)]
    [InlineData("https://ogma.techguypeter.com/", true)]
    [InlineData("http://www.techguypeter.com", false)]
    [InlineData("http://ogma.techguypeter.com/", false)]
    [InlineData("https://www.techguypeter.com/other", false)]
    [InlineData("https://example.com", false)]
    [InlineData("mailto:someone@example.com", false)]
    [InlineData("tel:+15555550100", false)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://www.techguypeter.com", false)]
    public void LinkPolicy_AllowsOnlyTheKnownHttpsMailtoAndTelAddresses(string address, bool allowed)
    {
        Assert.Equal(allowed, ExternalLinkPolicy.IsAllowed(new Uri(address, UriKind.Absolute)));
    }

    [Fact]
    public void LinkPolicy_RejectsNullAndRelativeAddresses()
    {
        Assert.False(ExternalLinkPolicy.IsAllowed(null));
        Assert.False(ExternalLinkPolicy.IsAllowed(new Uri("www.techguypeter.com", UriKind.Relative)));
    }

    [AvaloniaFact]
    public async Task Launcher_RefusesUnknownAddressesWithoutCallingThePlatform()
    {
        var window = new Window();
        var launcher = new AvaloniaExternalLinkLauncher(() => window);
        Assert.False(await launcher.TryOpenAsync(new Uri("https://example.com")));
        Assert.False(await launcher.TryOpenAsync(new Uri("file:///C:/Windows/System32/cmd.exe")));
        Assert.False(await new AvaloniaExternalLinkLauncher(() => null).TryOpenAsync(new Uri(AppCredits.WebsiteUrl)));
    }

    [Fact]
    public void Localization_HasEveryAboutKeyInEnglishAndFrench()
    {
        var localization = new InMemoryLocalizationService();
        var english = AboutKeys.ToDictionary(key => key, key => localization[key]);
        localization.SetCulture("fr");
        foreach (string key in AboutKeys)
        {
            Assert.DoesNotContain("⟦", english[key], StringComparison.Ordinal);
            Assert.DoesNotContain("⟦", localization[key], StringComparison.Ordinal);
        }

        Assert.Equal("Lead Developer", english["About.LeadDeveloper"]);
        Assert.Equal("Développeur principal", localization["About.LeadDeveloper"]);
        Assert.Equal("Fermer", localization["About.Close"]);
        Assert.Equal("À propos d'Ogma Library", localization["Command.App.About"]);
    }

    [Fact]
    public void ViewModel_ShowsVersionFromTheAssemblyAndTheCredit()
    {
        var viewModel = new AboutViewModel(new InMemoryLocalizationService(), new FakeLauncher(true), year: 2026);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Version));
        Assert.DoesNotContain("+", viewModel.Version, StringComparison.Ordinal);
        Assert.Equal("Version " + viewModel.Version, viewModel.VersionText);
        Assert.Equal("© 2026 Chwezi Core Systems", viewModel.CopyrightText);
        Assert.Equal("Lead Developer: Peter Bamuhigire", viewModel.LeadDeveloperAccessibleName);
        Assert.Equal("LEAD DEVELOPER", viewModel.LeadDeveloperEyebrow);
        Assert.Equal(LinkIds, viewModel.Links.Select(link => link.AutomationId));
        Assert.All(viewModel.Links, link => Assert.True(ExternalLinkPolicy.IsAllowed(link.Address)));
        Assert.Equal("Website: www.techguypeter.com", viewModel.Website.AccessibleName);
        Assert.Contains("Spectral", viewModel.LicencesText, StringComparison.Ordinal);

        var explicitVersion = new AboutViewModel(new InMemoryLocalizationService(), new FakeLauncher(true), version: "2.1.0+abc123");
        Assert.Equal("2.1.0", explicitVersion.Version);
    }

    [Fact]
    public async Task ViewModel_FailedOrThrowingLaunch_ShowsALocalisedMessageNotTheException()
    {
        var localization = new InMemoryLocalizationService();
        var refused = new AboutViewModel(localization, new FakeLauncher(false));
        Assert.False(await refused.OpenAsync(refused.Email));
        Assert.True(refused.HasStatusMessage);
        Assert.Equal(localization["About.LinkFailed"], refused.StatusMessage);

        var throwing = new AboutViewModel(localization, new FakeLauncher(new InvalidOperationException("raw platform detail")));
        Assert.False(await throwing.OpenAsync(throwing.Website));
        Assert.Equal(localization["About.LinkFailed"], throwing.StatusMessage);
        Assert.DoesNotContain("raw platform detail", throwing.StatusMessage, StringComparison.Ordinal);

        var launcher = new FakeLauncher(true);
        var opened = new AboutViewModel(localization, launcher);
        Assert.True(await opened.OpenAsync(opened.OgmaSite));
        Assert.False(opened.HasStatusMessage);
        Assert.Equal(new Uri(AppCredits.OgmaSiteUrl), Assert.Single(launcher.Opened));
    }

    [AvaloniaTheory]
    [InlineData("Light", "en")]
    [InlineData("Dark", "en")]
    [InlineData("Light", "fr")]
    [InlineData("Dark", "fr")]
    public void Dialog_RendersInBothThemesWithAccessibleControlsAndNoMissingText(string theme, string culture)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture(culture);
        var launcher = new FakeLauncher(true);
        var window = new AboutWindow
        {
            IsMotionAllowed = false,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = new AboutViewModel(localization, launcher, year: 2026),
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("About.Dialog", AutomationProperties.GetAutomationId(window));
        Assert.Equal(localization["About.Title"], AutomationProperties.GetName(window));

        string[] texts = [.. window.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)];
        Assert.DoesNotContain(texts, text => text.Contains('⟦', StringComparison.Ordinal));
        Assert.Contains("Ogma Library", texts);
        Assert.Contains("Peter Bamuhigire", texts);
        Assert.Contains("peter@techguypeter.com", texts);
        Assert.Contains("+256 784 464178", texts);
        Assert.Contains("ogma.techguypeter.com", texts);

        Button[] buttons = [.. window.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible)];
        Assert.Equal([.. LinkIds, "About.Close"], buttons.Select(AutomationProperties.GetAutomationId));
        foreach (Button button in buttons)
        {
            string? name = AutomationProperties.GetName(button);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.DoesNotContain("OgmaLibrary.", name, StringComparison.Ordinal);
            Assert.True(button.Focusable && button.IsTabStop);
            Assert.True(button.Bounds.Height >= 24, $"{AutomationProperties.GetAutomationId(button)} is too small.");
        }

        // Nothing is clipped horizontally: every text fits the dialog's width.
        foreach (TextBlock block in window.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible))
        {
            Assert.True(block.DesiredSize.Width <= window.Bounds.Width, $"'{block.Text}' overflows the dialog.");
        }

        // Activating a link goes through the allow-listed launcher.
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Uri(AppCredits.WebsiteUrl), Assert.Single(launcher.Opened));

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string directory = Path.Combine(RepositoryRoot(), "artifacts", "screenshots");
        Directory.CreateDirectory(directory);
        frame!.Save(Path.Combine(directory, $"about-dialog-{theme.ToLowerInvariant()}-{culture}.png"));
        window.Close();
    }

    [AvaloniaFact]
    public void Dialog_EscapeCloses()
    {
        var window = new AboutWindow
        {
            IsMotionAllowed = false,
            DataContext = new AboutViewModel(new InMemoryLocalizationService(), new FakeLauncher(true)),
        };
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(closed);
    }

    [AvaloniaFact]
    public void Tokens_TextMeetsAaContrastInBothThemes()
    {
        var app = new OgmaApp();
        app.Initialize();
        (string Foreground, string Background)[] textPairs =
        [
            ("About.Color.HeroText", "About.Color.HeroStart"),
            ("About.Color.HeroText", "About.Color.HeroEnd"),
            ("About.Color.HeroTagline", "About.Color.HeroMid"),
            ("About.Color.HeroTagline", "About.Color.HeroEnd"),
            ("About.Color.Eyebrow", "About.Color.Card"),
            ("About.Color.Text", "About.Color.Card"),
            ("About.Color.TextMuted", "About.Color.Card"),
            ("About.Color.Text", "About.Color.LinkHover"),
            ("About.Color.TextMuted", "About.Color.LinkHover"),
            ("About.Color.Text", "About.Color.LinkPressed"),
            ("About.Color.TextMuted", "About.Color.Footer"),
            ("About.Color.Eyebrow", "About.Color.Card"),
            ("About.Color.ButtonText", "About.Color.Button"),
            ("About.Color.ButtonText", "About.Color.ButtonHover"),
        ];
        string[] chips = ["About.Color.Chip.Lapis", "About.Color.Chip.Garnet", "About.Color.Chip.Malachite", "About.Color.Chip.Amber"];

        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            foreach ((string foreground, string background) in textPairs)
            {
                double ratio = ContrastRatio(ColorOf(app, foreground, theme), ColorOf(app, background, theme));
                Assert.True(ratio >= 4.5, $"{foreground} on {background} in {theme} is {ratio:F2}:1.");
            }

            foreach (string chip in chips)
            {
                double ratio = ContrastRatio(ColorOf(app, "About.Color.ChipGlyph", theme), ColorOf(app, chip, theme));
                Assert.True(ratio >= 3, $"Glyph on {chip} in {theme} is {ratio:F2}:1.");
            }
        }
    }

    [Fact]
    public void Dialog_AxamlUsesTokensOnly()
    {
        string axaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "OgmaLibrary.App", "Views", "About", "AboutWindow.axaml"));
        Assert.DoesNotMatch(@"FontSize=""\d", axaml);
        Assert.DoesNotMatch(@"(Foreground|Background|BorderBrush|Fill|Stroke)=""#", axaml);
        Assert.DoesNotMatch(@"Value=""#", axaml);
        Assert.DoesNotMatch(@"FontFamily=""", axaml);
        Assert.DoesNotMatch(@"\s(Text|Content|Title)=""[A-Za-z]", axaml);
        Assert.Matches(new Regex("About\\.Link\\.|AutomationId=\"About\\.Close\""), axaml);
    }

    private static Color ColorOf(OgmaApp app, string key, ThemeVariant theme)
    {
        Assert.True(app.TryGetResource(key, theme, out object? raw), $"Missing {key} for {theme}.");
        return Assert.IsType<Color>(raw);
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte channel)
            {
                double value = channel / 255d;
                return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
        }

        double a = Luminance(foreground);
        double b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static string RepositoryRoot()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "OgmaLibrary.sln")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Repository root not found.");
        }

        return root;
    }

    private sealed class FakeLauncher : IExternalLinkLauncher
    {
        private readonly bool _result;
        private readonly Exception? _failure;

        public FakeLauncher(bool result) => _result = result;

        public FakeLauncher(Exception failure) => _failure = failure;

        public List<Uri> Opened { get; } = [];

        public Task<bool> TryOpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (_failure is not null)
            {
                throw _failure;
            }

            Opened.Add(uri);
            return Task.FromResult(_result);
        }
    }
}

/// <summary>The About entry points: the palette command and the Settings button.</summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaFact]
    public async Task About_PaletteCommandAndSettingsButton_OpenTheDialog()
    {
        var localization = new InMemoryLocalizationService();
        SettingsHarness harness = CreateSettingsShell(localization);

        // Not offered until the application binds the dialog.
        Assert.DoesNotContain(harness.Shell.CommandPaletteItems, item => item.Id == "app.about");

        int shown = 0;
        harness.Shell.ShowAbout = _ =>
        {
            shown++;
            return Task.CompletedTask;
        };
        harness.Settings.ShowAbout = harness.Shell.ShowAbout;
        harness.Shell.OpenCommandPalette();
        Assert.Contains(harness.Shell.CommandPaletteItems, item => item.Id == "app.about");
        Assert.Equal("About Ogma Library", localization[harness.Shell.Commands.Find("app.about")!.LabelKey]);

        await harness.Shell.ExecuteCommandAsync("app.about");
        Assert.Equal(1, shown);

        (Window window, CatalogueShellView view) = ShowShell(harness.Shell, 1280, 800);
        harness.Shell.OpenSettings("library");
        Dispatcher.UIThread.RunJobs();
        Button about = view.GetVisualDescendants().OfType<SettingsView>().Single()
            .GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == "Settings.About");
        Assert.True(about.IsEffectivelyVisible);
        Assert.Equal("About Ogma Library", AutomationProperties.GetName(about));
        about.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, shown);

        window.Close();
        harness.Shell.Dispose();
    }
}
