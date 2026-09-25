using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Navigation;
using OgmaLibrary.App.Settings;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Settings;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.App.Views.Settings;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 08 (K16): the Settings destination renders every section with accessible
/// names, switches apply live and persist, French re-renders labels, and an environment
/// override is shown as a disabled control with an explanation.
/// </summary>
public sealed partial class ShellReaderNavigationTests
{
    private static readonly string[] SettingsSectionIds =
        ["library", "appearance", "language", "online", "features", "privacy", "ocr", "diagnostics"];

    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(1920, 1080)]
    public void Settings_EverySection_RendersWithAccessibleNamesAndTokensOnly(int width, int height)
    {
        var localization = new InMemoryLocalizationService();
        SettingsHarness harness = CreateSettingsShell(localization);
        (Window window, CatalogueShellView view) = ShowShell(harness.Shell, width, height);

        foreach (string id in SettingsSectionIds)
        {
            harness.Shell.OpenSettings(id);
            Dispatcher.UIThread.RunJobs();

            Assert.True(harness.Shell.IsSettingsActive);
            Assert.Equal(id, harness.Settings.SelectedSection.RouteId);
            SettingsView settingsView = view.GetVisualDescendants().OfType<SettingsView>().Single();
            Assert.True(settingsView.IsEffectivelyVisible);
            TextBlock title = settingsView.GetVisualDescendants().OfType<TextBlock>()
                .First(block => AutomationProperties.GetAutomationId(block) == "Settings.SectionTitle");
            Assert.Equal(harness.Settings.SelectedSection.Label, title.Text);
            Assert.False(title.Text!.StartsWith('⟦'));

            Control[] interactive = [.. settingsView.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible &&
                                  control is Button or RadioButton or CheckBox or ListBoxItem or TextBox)];
            Assert.NotEmpty(interactive);
            foreach (Control control in interactive)
            {
                string? name = AutomationProperties.GetName(control);
                Assert.False(string.IsNullOrWhiteSpace(name), $"A {control.GetType().Name} in section '{id}' has no accessible name.");
                Assert.DoesNotContain("OgmaLibrary.", name, StringComparison.Ordinal);
                Assert.False(name!.StartsWith('⟦'), $"'{name}' is a missing localization key.");
            }
        }

        window.Close();
        harness.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Settings_ThemeDensityAndCapability_ApplyLiveAndPersistAcrossRestart()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ogma-p08-ui-{Guid.NewGuid():N}");
        try
        {
            using (var store = new FileUserPreferencesService(directory))
            {
                SettingsHarness harness = CreateSettingsShell(new InMemoryLocalizationService(), store);
                await harness.Shell.InitializePreferencesAsync();
                var applied = new List<UserPreferences>();
                harness.Shell.UserPreferencesChanged += (_, preferences) => applied.Add(preferences);
                int capabilityChanges = 0;
                harness.Capabilities.Changed += (_, _) => capabilityChanges++;

                harness.Settings.IsThemeDark = true;
                harness.Settings.IsDensityCompact = true;
                harness.Settings.ClassroomHost.IsOn = true;
                await WaitForSettingsAsync(() => File.Exists(Path.Combine(directory, "user-preferences.json")) &&
                                         harness.Capabilities.IsClassroomHostEnabled);

                // Live: the shell (which re-applies theme and density) and capabilities follow at once.
                Assert.Equal(UserTheme.Dark, harness.Shell.Theme);
                Assert.Equal(UserDensity.Compact, harness.Shell.Density);
                Assert.Contains(applied, preferences => preferences.Theme == UserTheme.Dark);
                Assert.True(harness.Settings.IsThemeDark);
                Assert.False(harness.Settings.IsThemeLight);
                Assert.Equal(1, capabilityChanges);
                Assert.Contains(harness.Settings.CapabilityRows, row => row.IsAvailable);

                // The palette's theme command goes through the same controller.
                await harness.Shell.ToggleThemeAsync();
                Assert.True(harness.Settings.IsThemeSystem);
                harness.Shell.Dispose();
            }

            // Restart: a fresh store, capabilities and settings read the persisted choices.
            using var reopened = new FileUserPreferencesService(directory);
            SettingsHarness restarted = CreateSettingsShell(new InMemoryLocalizationService(), reopened);
            await restarted.Shell.InitializePreferencesAsync();
            Assert.Equal(UserTheme.System, restarted.Shell.Theme);
            Assert.True(restarted.Settings.IsDensityCompact);
            Assert.True(restarted.Settings.ClassroomHost.IsOn);
            Assert.True(restarted.Capabilities.IsClassroomHostEnabled);
            restarted.Shell.Dispose();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Settings_FrenchSwitch_ReRendersSettingsAndShellLabels()
    {
        var localization = new InMemoryLocalizationService();
        SettingsHarness harness = CreateSettingsShell(localization);
        await harness.Shell.InitializePreferencesAsync();
        (Window window, CatalogueShellView view) = ShowShell(harness.Shell, 1280, 800);
        harness.Shell.OpenSettings("language");
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(harness.Settings.Sections, item => item.Label == "Appearance");

        harness.Settings.IsLanguageFrench = true;
        await WaitForSettingsAsync(() => localization.CurrentCulture.TwoLetterISOLanguageName == "fr");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fr", harness.Preferences.Current.Culture);
        Assert.Contains(harness.Settings.Sections, item => item.Label == "Apparence");
        Assert.Equal("Langue", harness.Settings.SectionTitle);
        Assert.Contains(harness.Shell.RailItems, item => item.Destination == ShellDestination.Settings && item.Label == localization["Navigation.Settings"] && item.Label != "Settings");
        string[] visible = [.. view.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)];
        Assert.Contains("Langue de l'interface", visible);
        Assert.Contains(localization["Navigation.Library"], visible);
        Assert.DoesNotContain(visible, text => text.StartsWith('⟦'));

        window.Close();
        harness.Shell.Dispose();
    }

    [AvaloniaFact]
    public void Settings_EnvironmentOverride_ShowsADisabledSwitchWithTheReason()
    {
        var localization = new InMemoryLocalizationService();
        SettingsHarness harness = CreateSettingsShell(
            localization,
            overrides: new CapabilityOverrides(ThreeDimensionalShelf: true));
        (Window window, CatalogueShellView view) = ShowShell(harness.Shell, 1280, 800);
        harness.Shell.OpenSettings("features");
        Dispatcher.UIThread.RunJobs();

        CheckBox shelf = view.GetVisualDescendants().OfType<CheckBox>()
            .Single(box => AutomationProperties.GetAutomationId(box) == "Settings.Toggle.ThreeDimensionalShelf");
        CheckBox host = view.GetVisualDescendants().OfType<CheckBox>()
            .Single(box => AutomationProperties.GetAutomationId(box) == "Settings.Toggle.ClassroomHost");
        Assert.False(shelf.IsEnabled);
        Assert.True(shelf.IsChecked);
        Assert.True(host.IsEnabled);
        TextBlock reason = view.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => AutomationProperties.GetAutomationId(block) == "Settings.Toggle.ThreeDimensionalShelf.Managed");
        Assert.True(reason.IsEffectivelyVisible);
        Assert.Contains("OGMA_ENABLE_3D_SHELF", reason.Text, StringComparison.Ordinal);

        // The managed value cannot be changed from Settings.
        harness.Settings.Shelf3D.IsOn = false;
        Dispatcher.UIThread.RunJobs();
        Assert.True(harness.Capabilities.IsShelf3DAvailable);
        Assert.False(harness.Preferences.Current.EnableThreeDimensionalShelf);

        window.Close();
        harness.Shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Settings_PaletteOffersEverySection_AndRoutesAliasesToTheRightSection()
    {
        SettingsHarness harness = CreateSettingsShell(new InMemoryLocalizationService());
        string[] palette = [.. harness.Shell.CommandPaletteItems.Select(item => item.Id)];
        foreach (string id in SettingsSectionIds)
        {
            Assert.Contains("settings." + id, palette);
        }

        await harness.Shell.ExecuteCommandAsync("settings.diagnostics");
        Assert.True(harness.Settings.IsDiagnosticsSection);
        harness.Shell.OpenSettings("ai");
        Assert.True(harness.Settings.IsPrivacySection);
        harness.Shell.OpenSettings("classroom");
        Assert.True(harness.Settings.IsFeaturesSection);
        harness.Shell.OpenSettings();
        Assert.True(harness.Settings.IsFeaturesSection);
        harness.Shell.Dispose();
    }

    [Fact]
    public void Settings_ViewAxaml_UsesTokensOnly()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "OgmaLibrary.sln")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Repository root not found.");
        }

        string axaml = File.ReadAllText(Path.Combine(root, "src", "OgmaLibrary.App", "Views", "Settings", "SettingsView.axaml"));

        Assert.DoesNotMatch(@"FontSize=""\d", axaml);
        Assert.DoesNotMatch(@"(Foreground|Background|BorderBrush)=""#", axaml);
        Assert.DoesNotMatch(@"FontFamily=""", axaml);
        Assert.DoesNotMatch(@"(Text|Content)=""[A-Za-z]", axaml);
    }

    private static SettingsHarness CreateSettingsShell(
        InMemoryLocalizationService localization,
        IUserPreferencesService? store = null,
        CapabilityOverrides? overrides = null)
    {
        var capabilities = new RuntimeCapabilityState(overrides: overrides);
        var preferences = new UserPreferencesController(store, capabilities, localization, () => CultureInfo.GetCultureInfo("en-US"));
        var folders = new LibraryFoldersViewModel(new FakeRoots("Science"), new FakeMonitor(), new FakeAttention(0), localization);
        string data = Path.Combine(Path.GetTempPath(), "ogma-settings-test");
        var settings = new SettingsViewModel(
            localization,
            preferences,
            capabilities,
            new SettingsEnvironmentInfo("1.0.0-test", data, Path.Combine(data, "logs"), Path.Combine(data, ".ogma")),
            folders)
        {
            OpenFolder = _ => true,
        };
        var readModel = new EmptyCatalogueReadModel();
        var filter = new CatalogueFilterViewModel();
        var shell = new MainShellViewModel(
            localization,
            new CatalogueViewModel(readModel, new NullNavigation(), localization),
            new BookDetailViewModel(readModel, new NullNavigation(), localization),
            new ShelfSidebarViewModel(readModel, new NoOpCatalogueWriteService(), localization, filter),
            libraryFolders: folders,
            capabilities: capabilities,
            preferences: preferences,
            settings: settings);
        return new SettingsHarness(shell, settings, capabilities, preferences);
    }

    private static async Task WaitForSettingsAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition(), "The condition was not met within 2 s.");
    }

    private sealed record SettingsHarness(
        MainShellViewModel Shell,
        SettingsViewModel Settings,
        RuntimeCapabilityState Capabilities,
        UserPreferencesController Preferences);
}
