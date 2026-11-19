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

    /// <summary>
    /// Oracle (J-SET-3): a second folder added from Settings › Library through the native folder
    /// dialog is listed with its own rescan control, Rescan library completes, and the second
    /// folder's book joins the catalogue next to the first folder's books.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Settings")]
    [Trait("Tag", "Settings")]
    public void Settings_SecondFolderAddedFromSettings_JoinsCatalogueAfterRescan(string size) =>
        Journey.Run("Settings", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            int before = Shell.WaitForCatalogue(context, minimum: 1).Length;
            context.Record("catalogue.beforeSecondFolder", before);

            // The Settings panel opens the real native dialog (the OGMA_E2E_PICK_FOLDER hook only
            // covers the shell's Choose folder), so this drives the production picker path.
            string second = Path.Combine(context.Session.Root, "second-shelf");
            Directory.CreateDirectory(second);
            File.WriteAllBytes(Path.Combine(second, SecondFolderTitle + ".pdf"), MinimalPdf(SecondFolderTitle));

            OpenSection(context, "library");
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement add = WaitForButton(window, "Add another library folder");
            Visibility.AssertReachable(context.Window, add, "Settings › Library › Add folder");
            Uia.Activate(add);
            context.Record("library.secondPickerTechnique", FolderPicker.Pick(context.RequireApp, second));

            AutomationElement rescanSecond = WaitForButton(window, "Rescan folder second-shelf", TimeSpan.FromSeconds(20));
            Visibility.AssertReachable(context.Window, rescanSecond, "second folder's Rescan control");
            context.Shot("library-two-folders");

            Uia.Activate(WaitForButton(window, "Rescan library"));
            context.Mark("library.rescanPressed");

            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Library"));
            AutomationElement[] after = Shell.WaitForCatalogue(context, minimum: before + 1);
            context.Record("catalogue.afterSecondFolder", after.Length);
            Assert.True(
                Uia.Poll(() => Shell.FindCatalogueItem(context, SecondFolderTitle) is not null, TimeSpan.FromSeconds(60), intervalMs: 1000),
                $"The book from the second folder ('{SecondFolderTitle}') is not in the catalogue after Rescan library [J-SET-3].");
            context.Shot("catalogue-two-folders");
        });

    /// <summary>Oracle (Phase 08 evidence): every Settings section opens, shows its title and is captured.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Settings")]
    [Trait("Tag", "Settings")]
    public void Settings_EverySection_OpensAndIsCaptured(string size) =>
        Journey.Run("Settings", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            foreach (string section in SectionRoutes)
            {
                OpenSection(context, section);
                context.Shot("section-" + section);
            }
        });

    private const string SecondFolderTitle = "Second Shelf Primer";

    private static readonly string[] SectionRoutes =
        ["library", "appearance", "language", "online", "features", "privacy", "ocr", "diagnostics"];

    private static AutomationElement WaitForButton(AutomationElement window, string name, TimeSpan? timeout = null) =>
        Uia.WaitUntil(
            () => window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button))),
            timeout ?? Uia.DefaultTimeout,
            $"the '{name}' button");

    /// <summary>A one-page PDF with a text line and an Info title; distinct from every corpus file.</summary>
    private static byte[] MinimalPdf(string title)
    {
        string content = $"BT /F1 24 Tf 72 720 Td ({title}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Title ({title}) /Author (Ogma E2E) >>",
        ];
        var pdf = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        int xref = pdf.Length;
        pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 6 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(pdf.ToString());
    }

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
