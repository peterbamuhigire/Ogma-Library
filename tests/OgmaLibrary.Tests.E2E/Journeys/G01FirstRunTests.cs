using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G1: first run — the empty state and its call to action are visible and reachable.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G01FirstRunTests
{
    /// <summary>Oracle: <c>AssertVisiblyPainted</c> on the heading and the Choose-folder button (K10, K11).</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G1")]
    [Trait("Tag", "Catalogue")]
    [Trait("Tag", "Visual")]
    public void G1_FirstRun_EmptyStateAndCallToActionAreVisible(string size) =>
        Journey.Run("G1", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement heading = Uia.WaitFor(window, "Catalogue.EmptyState.Heading", TimeSpan.FromSeconds(60));
            context.Mark("emptyState.present");
            AutomationElement cta = Uia.WaitFor(window, "Catalogue.EmptyState.ChooseFolder");
            AutomationElement toolbarCta = Uia.WaitFor(window, "Shell.Action.ChooseFolder");

            VisibilityReport headingReport = Visibility.AssertVisiblyPainted(context.Window, heading, "Empty-state heading");
            VisibilityReport ctaReport = Visibility.AssertVisiblyPainted(context.Window, cta, "Empty-state Choose folder button");
            Visibility.AssertReachable(context.Window, cta, "Empty-state Choose folder button");
            Visibility.AssertReachable(context.Window, toolbarCta, "Toolbar Choose folder button");
            context.Mark("emptyState.verified");
            context.Record("visibility.heading", headingReport.ToString());
            context.Record("visibility.cta", ctaReport.ToString());
            context.Dump("first-run");
        });
}
