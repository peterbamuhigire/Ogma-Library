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
            var status = new StatusSamples();
            Uia.Poll(
                () =>
                {
                    status.Sample(context);
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

            AssertInspectorShowsIsbn(context, oracle);
            AssertProcessingFinishes(context, status);
        });

    /// <summary>
    /// Phase 06: the status bar counts tasks, never shows "N of M" with N &gt; M, and leaves the
    /// "Preparing books" state for the finished library summary.
    /// </summary>
    private static void AssertProcessingFinishes(JourneyContext context, StatusSamples status)
    {
        bool finished = Uia.Poll(
            () =>
            {
                status.Sample(context);
                return status.Last is { Length: > 0 } last && !StatusSamples.IsPreparing(last);
            },
            TimeSpan.FromSeconds(180),
            intervalMs: 1000);
        context.Mark("processing.finished");
        context.Record("status.samples", status.Count);
        context.Record("status.preparingSamples", status.Preparing);
        context.Record("status.overflowSamples", status.Overflow);
        context.Record("status.maxTotal", status.MaxTotal);
        context.Record("status.final", status.Last);
        context.Shot("processing-finished");
        Assert.True(status.Overflow == 0, $"The status bar showed N > M in {status.Overflow} of {status.Count} samples (Phase 06).");
        Assert.True(finished, $"Processing did not reach the finished summary within 180 s; last status '{status.Last}' (Phase 06).");
    }

    /// <summary>Samples <c>Shell.Status</c> and counts "Preparing books: N of M tasks" overflows.</summary>
    private sealed class StatusSamples
    {
        private static readonly System.Text.RegularExpressions.Regex Progress =
            new(@"(\d+)\D+(\d+)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        public int Count { get; private set; }

        public int Preparing { get; private set; }

        public int Overflow { get; private set; }

        public int MaxTotal { get; private set; }

        public string? Last { get; private set; }

        public static bool IsPreparing(string text) => text.StartsWith("Preparing books", StringComparison.Ordinal);

        public void Sample(JourneyContext context)
        {
            try
            {
                if (Uia.TryFind(context.RequireApp.MainWindow, "Shell.Status") is not { } label)
                {
                    return;
                }

                string text = Uia.GetText(label) ?? label.Properties.Name.ValueOrDefault ?? string.Empty;
                Last = text;
                Count++;
                if (!IsPreparing(text) || Progress.Match(text) is not { Success: true } m)
                {
                    return;
                }

                Preparing++;
                int done = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                int total = int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                MaxTotal = Math.Max(MaxTotal, total);
                if (done > total)
                {
                    Overflow++;
                }
            }
            catch (Exception ex) when (Uia.IsTransient(ex))
            {
                // A stale element during a progressive refresh; the next sample reads it again.
            }
        }
    }

    /// <summary>
    /// Phase 06: the detail inspector shows the scanned ISBN of a corpus book that has one. The
    /// ISBN line has no AutomationId, so it is found by its visible text (<c>ISBN: …</c>) and
    /// compared digit for digit with expected.json. An empty binding fails.
    /// </summary>
    private static void AssertInspectorShowsIsbn(JourneyContext context, CorpusOracle oracle)
    {
        CorpusBook book = oracle.Catalogue.First(b => b.IsbnValid && b.Isbn is not null && b.Pages <= 60);
        string expectedDigits = Digits(book.Isbn!);
        AutomationElement window = context.RequireApp.MainWindow;
        AutomationElement item = Uia.WaitUntil(
            () => Shell.FindCatalogueItem(context, book.Title!),
            TimeSpan.FromSeconds(30),
            $"catalogue item '{book.Title}'");
        item.Patterns.SelectionItem.Pattern.Select();
        Uia.WaitFor(window, "Detail.Action.Close", TimeSpan.FromSeconds(30));

        // The ISBN is on the inspector's second tab group (Bibliographic; File is the default).
        // The tabs have no AutomationId, so the seven-group tab control is found by shape.
        AutomationElement biblio = Uia.WaitUntil(
            () => window
                .FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab))
                .Select(t => t.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)))
                .FirstOrDefault(items => items.Length >= 7)?[1],
            TimeSpan.FromSeconds(15),
            "the inspector's Bibliographic tab");
        context.Record("detail.isbn.tab", biblio.Properties.Name.ValueOrDefault);
        biblio.Patterns.SelectionItem.Pattern.Select();

        string? shown = null;
        Uia.Poll(
            () =>
            {
                shown = window
                    .FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text))
                    .Select(e => e.Properties.Name.ValueOrDefault)
                    .FirstOrDefault(n => n is not null && n.StartsWith("ISBN", StringComparison.OrdinalIgnoreCase));
                return shown is not null && Digits(shown).EndsWith(expectedDigits, StringComparison.Ordinal);
            },
            TimeSpan.FromSeconds(60),
            intervalMs: 1000);
        context.Record("detail.isbn.book", book.Title);
        context.Record("detail.isbn.shown", shown);
        context.Shot("detail-isbn");
        Assert.True(
            shown is not null && Digits(shown).EndsWith(expectedDigits, StringComparison.Ordinal),
            $"The detail inspector for '{book.Title}' shows '{shown ?? "(no ISBN line)"}'; expected ISBN {book.Isbn} (Phase 06).");
    }

    private static string Digits(string value) =>
        new(value.Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray());
}
