using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G2: add a library through the real folder dialog, scan, and see the valid books with covers.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G02AddLibraryTests
{
    /// <summary>
    /// Oracle: the grid is visibly painted, its item count equals the valid works in expected.json
    /// (K21) and at least 80 % of the cards show a real cover image, not the placeholder (K20).
    /// Uses the real native dialog (G1b technique) so the production picker path is exercised.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G2")]
    [Trait("Tag", "Catalogue")]
    [Trait("Tag", "Visual")]
    public void G2_AddLibrary_ScanShowsValidBooksWithCovers(string size) =>
        Journey.Run("G2", size, JourneySupport.Standard, context =>
        {
            CorpusOracle oracle = Corpus.Oracle;
            context.Launch();
            Shell.WaitReady(context);
            PickerRoute route = Shell.AddCorpusLibrary(context, "Catalogue.EmptyState.ChooseFolder");
            Assert.Equal(PickerRoute.Dialog, route);

            AutomationElement[] items = Shell.WaitForCatalogue(context, minimum: 1);
            AutomationElement grid = Uia.WaitFor(context.RequireApp.MainWindow, "Catalogue.Grid");
            Visibility.AssertVisiblyPainted(context.Window, grid, "Catalogue grid");
            Visibility.AssertVisiblyPainted(context.Window, items[0], "First catalogue card");
            context.Shot("scanned");

            // Covers are generated after the scan; give them time before judging.
            double coverShare = 0;
            Uia.Poll(
                () =>
                {
                    AutomationElement[] current = Shell.CatalogueItems(context);
                    int withCover = current.Count(Shell.HasCoverImage);
                    coverShare = current.Length == 0 ? 0 : withCover / (double)current.Length;
                    return coverShare >= 0.8;
                },
                TimeSpan.FromSeconds(90),
                intervalMs: 2000);
            items = Shell.CatalogueItems(context);
            context.Record("catalogue.finalCount", items.Length);
            context.Record("catalogue.names", items.Select(i => i.Properties.Name.ValueOrDefault).ToArray());
            context.Record("covers.share", coverShare);
            context.Dump("scanned");

            // Flagged cards (for example a Locked, password-protected PDF) are honest placeholders,
            // not valid works; G6 checks them. Count only unflagged cards against the oracle.
            // The catalogue refreshes progressively (Phase 06), so re-read the cards until one
            // consistent snapshot can be inspected without stale UIA elements.
            int flagged = 0;
            int valid = 0;
            Uia.Poll(
                () =>
                {
                    try
                    {
                        AutomationElement[] snapshot = Shell.CatalogueItems(context);
                        flagged = snapshot.Count(Shell.IsFlaggedCard);
                        valid = snapshot.Length - flagged;
                        return valid == oracle.CatalogueCount;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(30),
                intervalMs: 1000);
            context.Record("catalogue.flaggedCount", flagged);
            Assert.True(
                valid == oracle.CatalogueCount,
                $"The catalogue shows {valid} unflagged books ({flagged} flagged); expected.json lists {oracle.CatalogueCount} valid works " +
                $"({oracle.Invalid.Count} invalid files and 1 duplicate must not add cards) [K21].");
            Assert.True(coverShare >= 0.8, $"Only {coverShare:P0} of the cards show a cover image (target 80 %) [K20].");
        });
}
