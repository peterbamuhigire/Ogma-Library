using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Sept-23 Phase 17 journey "ScanOcrSearch": scan the corpus, the two image-only PDFs are shown
/// honestly (never "Indexed"), automatic OCR makes them searchable, their cards say "OCR text"
/// with a confidence, and the audit phrase finds the scanned pamphlet from OCR text (K21).
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class ScanOcrSearchTests
{
    private static readonly string[] BadgeIds =
        ["Indexed", "Searchable", "ImageOnly", "OcrInProgress", "OcrText", "OcrFailed", "NoText"];

    private static readonly (string Title, string Phrase)[] ScannedBooks =
    [
        ("Scanned Pamphlet", "amber library lantern"),
        ("Scanned Handout", "rareocrkeyword"),
    ];

    /// <summary>
    /// Oracle: each scanned card ends with the <c>Catalogue.Item.Badge.OcrText</c> badge and no
    /// <c>Catalogue.Item.Badge.Indexed</c>/<c>Searchable</c> badge before OCR; a search for its
    /// phrase lists it in the top three with the "From OCR text" label.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "ScanOcrSearch")]
    [Trait("Tag", "Search")]
    [Trait("Tag", "Ocr")]
    public void ScanOcrSearch_ScannedBooksBecomeSearchableFromOcrText(string size) =>
        Journey.Run("ScanOcrSearch", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);

            var failures = new List<string>();
            var observed = new List<object>();
            foreach ((string title, _) in ScannedBooks)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                bool ocrText = Uia.Poll(
                    () =>
                    {
                        AutomationElement? card = Shell.FindCatalogueItem(context, title);
                        if (card is null)
                        {
                            return false;
                        }

                        foreach (string badge in BadgeIds)
                        {
                            if (card.FindFirstDescendant(cf => cf.ByAutomationId($"Catalogue.Item.Badge.{badge}")) is not null)
                            {
                                seen.Add(badge);
                            }
                        }

                        return seen.Contains("OcrText");
                    },
                    TimeSpan.FromMinutes(4),
                    intervalMs: 1000);
                observed.Add(new { title, badges = seen.ToArray() });
                if (!ocrText)
                {
                    failures.Add($"'{title}' never showed the OCR text badge (seen: {string.Join(", ", seen)}) [K21]");
                }

                if (seen.Contains("Indexed") || seen.Contains("Searchable"))
                {
                    failures.Add($"'{title}' was badged as searchable before OCR (seen: {string.Join(", ", seen)}) [K21]");
                }
            }

            context.Record("ocr.badges", observed);
            context.Shot("ocr-badges");

            AutomationElement window = context.RequireApp.MainWindow;
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Search"));
            AutomationElement box = Uia.WaitFor(window, "Search.Box");
            var searches = new List<object>();
            foreach ((string title, string phrase) in ScannedBooks)
            {
                Uia.SetText(box, phrase);
                AutomationElement? match = null;
                bool found = Uia.Poll(
                    () =>
                    {
                        AutomationElement? results = Uia.TryFind(window, "Search.Results");
                        match = results is null
                            ? null
                            : Uia.ListItems(results).Take(3).FirstOrDefault(item =>
                                (item.Properties.Name.ValueOrDefault ?? string.Empty).Contains(title, StringComparison.OrdinalIgnoreCase));
                        return match is not null;
                    },
                    TimeSpan.FromSeconds(30),
                    intervalMs: 300);
                bool labelled = match?.FindFirstDescendant(cf => cf.ByName("From OCR text")) is not null;
                searches.Add(new { phrase, title, found, labelled });
                if (!found)
                {
                    failures.Add($"search '{phrase}' did not list '{title}' in the top three [K21]");
                }
                else if (!labelled)
                {
                    failures.Add($"search '{phrase}' found '{title}' without the 'From OCR text' label");
                }
            }

            context.Record("ocr.searches", searches);
            context.Shot("ocr-search");
            Assert.True(failures.Count == 0, "Scanned-book OCR journey: " + string.Join("; ", failures));
        });
}
