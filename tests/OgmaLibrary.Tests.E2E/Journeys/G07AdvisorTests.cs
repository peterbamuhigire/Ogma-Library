using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G7: ask the Advisor a question with AI unconfigured.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G07AdvisorTests
{
    /// <summary>
    /// Oracle: the Advisor panel is visible, and after asking with no AI provider configured the
    /// user sees a helpful, localised state with a reachable route to Settings
    /// (<c>Advisor.SettingsRoute</c>) — never a dead end.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G7")]
    [Trait("Tag", "Ai")]
    public void G7_Advisor_UnconfiguredShowsRouteToSettings(string size) =>
        Journey.Run("G7", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Advisor"));
            AutomationElement query = Uia.WaitFor(window, "Advisor.Query");
            Visibility.AssertVisiblyPainted(context.Window, query, "Advisor question box");
            Uia.SetText(query, "Which book explains refraction and rainbows?");
            AutomationElement ask = Uia.WaitFor(window, "Advisor.Ask");
            bool askEnabled = Uia.Poll(() => ask.IsEnabled, TimeSpan.FromSeconds(3));
            context.Record("advisor.askEnabled", askEnabled);
            if (askEnabled)
            {
                Uia.Activate(ask);
            }

            AutomationElement? route = null;
            Uia.Poll(() => (route = Uia.TryFind(window, "Advisor.SettingsRoute")) is not null, TimeSpan.FromSeconds(15));
            string[] visibleText = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Where(t => !t.Properties.IsOffscreen.ValueOrDefault)
                .Select(t => t.Properties.Name.ValueOrDefault ?? string.Empty)
                .Where(t => t.Length > 0)
                .ToArray();
            context.Record("advisor.visibleText", visibleText);
            context.Shot("advisor");
            Assert.True(route is not null,
                $"Dead end: no route to Settings (Advisor.SettingsRoute) after asking with AI unconfigured (Ask enabled: {askEnabled}) [G7, Phases 15-16].");
            Visibility.AssertReachable(context.Window, route!, "Advisor route to Settings");
        });
}
