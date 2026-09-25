using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G6: 0-byte, non-PDF, truncated and password-protected files are handled honestly.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G06InvalidFilesTests
{
    private static readonly string[] AttentionBadges =
        ["Catalogue.Item.Badge.IndexFailed", "Catalogue.Item.Badge.Unavailable", "Catalogue.Item.Attention",
         // A password-protected PDF is a book shown with a Locked badge (ADR-0018, Phase 05).
         "Catalogue.Item.Badge.Locked"];

    /// <summary>
    /// Oracle: no invalid file appears as a normal indexed book. A card that is shown must carry a
    /// needs-attention indicator (failed / unavailable / attention) and no Indexed badge; a file
    /// that is not shown must be surfaced by a library-level needs-attention control.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G6")]
    [Trait("Tag", "Catalogue")]
    public void G6_InvalidFiles_AreFlaggedNotShownAsBooks(string size) =>
        Journey.Run("G6", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            AutomationElement window = context.RequireApp.MainWindow;

            List<string> failures = [];
            Uia.Poll(
                () =>
                {
                    failures = Evaluate(context, window);
                    return failures.Count == 0;
                },
                TimeSpan.FromSeconds(90),
                intervalMs: 3000);
            context.Record("invalid.failures", failures);
            context.Dump("invalid-files");
            Assert.True(failures.Count == 0, "Invalid files presented as books [K21]: " + string.Join("; ", failures));
        });

    private static List<string> Evaluate(JourneyContext context, AutomationElement window)
    {
        var failures = new List<string>();
        foreach (CorpusInvalid invalid in Corpus.Oracle.Invalid)
        {
            AutomationElement? card = Shell.FindCatalogueItem(context, invalid.DisplayHint);
            if (card is null)
            {
                if (Uia.TryFind(window, "Catalogue.NeedsAttention") is null)
                {
                    failures.Add($"{invalid.Reason} file '{invalid.DisplayHint}' is hidden with no needs-attention indicator");
                }

                continue;
            }

            bool indexed = card.FindFirstDescendant(cf => cf.ByAutomationId("Catalogue.Item.Badge.Indexed")) is not null;
            bool flagged = AttentionBadges.Any(id => card.FindFirstDescendant(cf => cf.ByAutomationId(id)) is not null);
            if (indexed || !flagged)
            {
                failures.Add($"{invalid.Reason} file '{invalid.DisplayHint}' is shown as a normal book (indexed={indexed}, flagged={flagged})");
            }
        }

        return failures;
    }
}
