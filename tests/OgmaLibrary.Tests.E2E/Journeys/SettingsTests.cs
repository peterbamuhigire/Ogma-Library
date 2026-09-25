using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Sept-23 Phase 08 (K16, J-SET-1): the Settings destination in the real window. Theme, density
/// and language change from Settings, apply at once and survive a restart; an environment
/// override shows its switch disabled with the reason.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class SettingsTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>Oracle: Dark + Compact + Français are applied live, persisted and restored after relaunch.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Settings")]
    [Trait("Tag", "Settings")]
    public void Settings_ThemeDensityAndLanguage_ApplyLiveAndPersistAcrossRestart(string size) =>
        Journey.Run("Settings", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;
            string englishSettings = Uia.WaitFor(window, "Shell.Nav.Settings").Properties.Name.ValueOrDefault ?? string.Empty;
            string englishLibrary = Uia.WaitFor(window, "Shell.Nav.Library").Properties.Name.ValueOrDefault ?? string.Empty;

            OpenSection(context, "appearance");
            Choose(window, "Settings.Theme.Dark");
            Choose(window, "Settings.Density.Compact");
            Assert.True(Uia.Poll(() => IsChosen(Uia.WaitFor(window, "Settings.Theme.Dark")), TimeSpan.FromSeconds(5)), "Dark theme was not selected [K16].");
            context.Shot("appearance-dark");

            OpenSection(context, "language");
            Choose(window, "Settings.Language.French");
            bool french = Uia.Poll(
                () => Uia.TryFind(window, "Shell.Nav.Settings")?.Properties.Name.ValueOrDefault is { } name &&
                      !string.Equals(name, englishSettings, StringComparison.Ordinal),
                TimeSpan.FromSeconds(10));
            Assert.True(french, "Choosing Français did not change the visible navigation labels [K16].");
            string frenchLibrary = Uia.WaitFor(window, "Shell.Nav.Library").Properties.Name.ValueOrDefault ?? string.Empty;
            Assert.NotEqual(englishLibrary, frenchLibrary);
            context.Record("settings.labels", new { englishSettings, englishLibrary, frenchLibrary });
            context.Shot("language-fr");

            string preferencesPath = Path.Combine(context.Session.DataDirectory, "user-preferences.json");
            Assert.True(Uia.Poll(() => ReadPreferences(preferencesPath) is { } p && p.Theme == 1 && p.Density == 1 && p.Culture == "fr", TimeSpan.FromSeconds(5)),
                "user-preferences.json does not hold Dark, Compact and fr.");

            context.Launch(label: "relaunch");
            Shell.WaitReady(context);
            window = context.RequireApp.MainWindow;
            Assert.Equal(frenchLibrary, Uia.WaitFor(window, "Shell.Nav.Library").Properties.Name.ValueOrDefault);
            OpenSection(context, "appearance");
            Assert.True(IsChosen(Uia.WaitFor(window, "Settings.Theme.Dark")), "Dark theme was not restored after restart.");
            Assert.True(IsChosen(Uia.WaitFor(window, "Settings.Density.Compact")), "Compact density was not restored after restart.");
            context.Shot("after-restart");
        });

    /// <summary>Oracle: with OGMA_ENABLE_3D_SHELF set, the 3D switch is on, disabled and names the variable.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Settings")]
    [Trait("Tag", "Settings")]
    public void Settings_EnvironmentOverride_IsShownDisabledWithTheReason(string size) =>
        Journey.Run("Settings", size, JourneySupport.Standard, context =>
        {
            context.Launch(new Dictionary<string, string>(StringComparer.Ordinal) { ["OGMA_ENABLE_3D_SHELF"] = "1" });
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;

            OpenSection(context, "features");
            AutomationElement shelf = Uia.WaitFor(window, "Settings.Toggle.ThreeDimensionalShelf");
            AutomationElement host = Uia.WaitFor(window, "Settings.Toggle.ClassroomHost");
            Assert.False(shelf.Properties.IsEnabled.ValueOrDefault, "An environment-managed switch is editable [K16].");
            Assert.True(host.Properties.IsEnabled.ValueOrDefault, "A user-controlled switch is disabled.");
            Assert.True(IsChosen(shelf), "The environment override is not reflected in the switch.");
            string reason = Uia.WaitFor(window, "Settings.Toggle.ThreeDimensionalShelf.Managed").Properties.Name.ValueOrDefault ?? string.Empty;
            Assert.Contains("OGMA_ENABLE_3D_SHELF", reason, StringComparison.Ordinal);
            Visibility.AssertVisiblyPainted(context.Window, Uia.WaitFor(window, "Settings.Toggle.ThreeDimensionalShelf.Managed"), "managed-by-environment explanation");
            context.Shot("features-managed");
        });

    private static void OpenSection(JourneyContext context, string section)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Settings"));
        AutomationElement item = Uia.WaitFor(window, "Settings.Section." + section);
        Visibility.AssertReachable(context.Window, item, "Settings section " + section);
        Uia.Activate(item);
        Assert.True(
            Uia.Poll(() => Uia.TryFind(window, "Settings.SectionTitle")?.Properties.Name.ValueOrDefault == item.Properties.Name.ValueOrDefault, TimeSpan.FromSeconds(5)),
            $"Settings did not show the '{section}' section.");
    }

    private static void Choose(AutomationElement window, string automationId)
    {
        AutomationElement option = Uia.WaitFor(window, automationId);
        if (!IsChosen(option))
        {
            Uia.Activate(option);
        }
    }

    private static bool IsChosen(AutomationElement element) =>
        element.Patterns.SelectionItem.TryGetPattern(out var selection)
            ? selection.IsSelected.ValueOrDefault
            : element.Patterns.Toggle.TryGetPattern(out var toggle) && toggle.ToggleState.ValueOrDefault == ToggleState.On;

    private static StoredPreferences? ReadPreferences(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<StoredPreferences>(File.ReadAllText(path), WebJson)
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // The app may be replacing the file; the caller polls again.
            return null;
        }
    }

    private sealed record StoredPreferences(int Theme, int Density, string? Culture);
}
