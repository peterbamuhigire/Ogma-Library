using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G4: search by title, author, body phrase, typo and Unicode.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G04SearchTests
{
    /// <summary>Oracle: the expected book is in the top three results for each expected.json query.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G4")]
    [Trait("Tag", "Search")]
    public void G4_Search_ExpectedBookInTopThree(string size) =>
        Journey.Run("G4", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            AutomationElement window = context.RequireApp.MainWindow;
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Search"));
            AutomationElement box = Uia.WaitFor(window, "Search.Box");
            Visibility.AssertVisiblyPainted(context.Window, box, "Search box");

            var failures = new List<string>();
            var outcomes = new List<object>();
            bool first = true;
            foreach (CorpusSearch probe in Corpus.Oracle.Searches)
            {
                // The first query also waits for the index built after the scan.
                TimeSpan wait = first ? TimeSpan.FromSeconds(90) : TimeSpan.FromSeconds(20);
                first = false;
                var clock = Stopwatch.StartNew();
                Uia.SetText(box, probe.Query);
                string[] top = [];
                bool found = Uia.Poll(
                    () =>
                    {
                        top = TopResults(window);
                        return top.Any(name => name.Contains(probe.ExpectTitle, StringComparison.OrdinalIgnoreCase));
                    },
                    wait,
                    intervalMs: 300);
                outcomes.Add(new { probe.Kind, probe.Query, probe.ExpectTitle, found, ms = clock.ElapsedMilliseconds, top });
                if (!found)
                {
                    failures.Add($"{probe.Kind} '{probe.Query}' → top 3 [{string.Join(" | ", top)}], expected '{probe.ExpectTitle}'");
                }
            }

            context.Record("search.outcomes", outcomes);
            context.Shot("search");
            Assert.True(failures.Count == 0, "Search misses: " + string.Join("; ", failures));
        });

    private static string[] TopResults(AutomationElement window)
    {
        AutomationElement? results = Uia.TryFind(window, "Search.Results");
        return results is null
            ? []
            : Uia.ListItems(results).Take(3).Select(item => item.Properties.Name.ValueOrDefault ?? string.Empty).ToArray();
    }
}
