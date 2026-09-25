using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>T01.7 / K14: list items announce readable names, never a C# record dump.</summary>
[Collection(RealWindowTests.Name)]
public sealed class AccessibleNamesTests
{
    /// <summary>Catalogue cards have non-empty names that are not record dumps.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "A11yNames")]
    [Trait("Tag", "Accessibility")]
    [Trait("Tag", "Catalogue")]
    public void CatalogueItems_HaveReadableAccessibleNames(string size) =>
        Journey.Run("A11yNames", size, JourneySupport.Standard, evidenceFolder: "catalogue", body: context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            AutomationElement[] catalogue = Shell.WaitForCatalogue(context, minimum: 1, stableFor: TimeSpan.FromSeconds(3));
            string[] names = catalogue.Select(i => i.Properties.Name.ValueOrDefault ?? string.Empty).ToArray();
            context.Record("names.catalogue", names);
            AssertReadable(names);
        });

    /// <summary>Search result items have non-empty names that are not record dumps.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "A11yNames")]
    [Trait("Tag", "Accessibility")]
    [Trait("Tag", "Search")]
    public void SearchResults_HaveReadableAccessibleNames(string size) =>
        Journey.Run("A11yNames", size, JourneySupport.Standard, evidenceFolder: "search", body: context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1, stableFor: TimeSpan.FromSeconds(3));
            AutomationElement window = context.RequireApp.MainWindow;
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Search"));
            AutomationElement box = Uia.WaitFor(window, "Search.Box");
            AutomationElement[] results = [];
            // Single words are the queries search answers most reliably (K40); retype to refresh (K41).
            Uia.Poll(
                () =>
                {
                    Uia.SetText(box, string.Empty);
                    Uia.SetText(box, "Lantern");
                    Thread.Sleep(1500);
                    results = Uia.TryFind(window, "Search.Results") is { } list ? Uia.ListItems(list) : [];
                    return results.Length > 0;
                },
                TimeSpan.FromSeconds(90),
                intervalMs: 1000);
            string[] names = results.Select(i => i.Properties.Name.ValueOrDefault ?? string.Empty).ToArray();
            context.Record("names.search", names);
            context.Dump("search-names");
            Assert.True(results.Length > 0, "The search for 'Lantern' returned no list items to audit [K40, K41].");
            AssertReadable(names);
        });

    private static void AssertReadable(string[] names)
    {
        string[] bad = names.Where(name => string.IsNullOrWhiteSpace(name) || UiaAudit.IsRecordDump(name)).ToArray();
        Assert.True(bad.Length == 0, "Unreadable list-item names [K14]: " + string.Join(" | ", bad.Select(n => n.Length > 80 ? n[..80] : n)));
    }
}
