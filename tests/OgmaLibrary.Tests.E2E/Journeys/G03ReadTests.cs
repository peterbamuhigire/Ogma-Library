using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G3: select → detail inspector → Read → turn 50 pages → back to the library.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G03ReadTests
{
    /// <summary>The Phase 04 page-turn budget to a preview (NFR-OGMA-005): p95 ≤ 250 ms.</summary>
    public const double PageTurnP95BudgetMs = 250;

    /// <summary>
    /// A rendered title page is mostly white (measured 0.7 % non-background on page 1 of the probe
    /// book); an unrendered page has no image element at all, so a low floor is enough.
    /// </summary>
    public const double ReaderPageMinimumPainted = 0.002;

    /// <summary>Oracle: no crash; page-turn p95 within budget; the page region is painted; the library comes back.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G3")]
    [Trait("Tag", "Reader")]
    [Trait("Tag", "Detail")]
    public void G3_Read_TurnFiftyPagesAndReturn(string size) =>
        Journey.Run("G3", size, JourneySupport.Standard, context =>
        {
            CorpusReader probe = Corpus.Oracle.Reader;
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            Shell.OpenInReader(context, probe.Title);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement page = Uia.WaitFor(window, "Reader.Page");
            AssertPagePainted(context, page, "first page");
            context.Mark("reader.firstPagePainted");
            context.Shot("reader-open");

            int turns = Math.Min(50, probe.Pages - 1);
            var latencies = new List<double>(turns);
            int ignoredPresses = 0;
            PageTurn? stalled = null;
            for (int turn = 0; turn < turns; turn++)
            {
                PageTurn result = Shell.TurnPage(context);
                ignoredPresses += result.IgnoredPresses;
                Assert.True(context.RequireApp.IsAlive, $"The app exited during page turn {turn + 1}.");
                if (!result.Advanced)
                {
                    stalled = result;
                    break;
                }

                latencies.Add(result.Milliseconds);
            }

            context.Record("pageTurn.ignoredPresses", ignoredPresses);
            context.Record("pageTurn.count", latencies.Count);
            context.Record("pageTurn.latenciesMs", latencies.Select(l => Math.Round(l)).ToArray());
            context.Record("pageTurn.method", "Reader.Next Invoke until Reader.PageNumber shows the next page (UIA round trips included)");
            double p95 = double.NaN;
            if (latencies.Count > 0)
            {
                var sorted = latencies.OrderBy(l => l).ToList();
                p95 = sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
                context.Record("pageTurn.p50Ms", sorted[sorted.Count / 2]);
                context.Record("pageTurn.p95Ms", p95);
                context.Record("pageTurn.maxMs", sorted[^1]);
            }

            Assert.True(stalled is null, $"Page turn from page {stalled?.From} did not advance within 10 s ({latencies.Count} turns completed, p95 {p95:0} ms, {ignoredPresses} presses ignored) [K32].");
            AssertPagePainted(context, Uia.WaitFor(window, "Reader.Page"), $"page {turns + 1}");
            context.Shot("reader-after-turns");

            Uia.Activate(Uia.WaitFor(window, "Reader.BackToLibrary"));
            AutomationElement grid = Uia.WaitFor(window, "Catalogue.Grid");
            Visibility.AssertVisiblyPainted(context.Window, grid, "Catalogue grid after returning from the reader");
            context.Mark("library.returned");
            Assert.True(p95 <= PageTurnP95BudgetMs, $"Page-turn p95 is {p95:0} ms (budget {PageTurnP95BudgetMs:0} ms) [K32, Phase 04].");
        });

    internal static void AssertPagePainted(JourneyContext context, AutomationElement page, string what)
    {
        VisibilityReport? report = null;
        bool painted = Uia.Poll(
            () =>
            {
                report = Visibility.Measure(context.Window, page, minimumPainted: ReaderPageMinimumPainted, allowScrolledPartially: true);
                return report.Passed;
            },
            TimeSpan.FromSeconds(20),
            intervalMs: 500);
        context.Record("visibility." + what, report?.ToString());
        Assert.True(painted, $"The reader {what} is not visibly painted: {report}");
    }
}
