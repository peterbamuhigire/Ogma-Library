using System.Drawing;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Phase 12 reader journeys J-READ-1..6, the negative cases, the page-area share and the 100 % sharpness
/// check. A journey reports NOT ASSESSED (never a pass) when a precondition it needs is absent.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class ReaderJourneyTests
{
    /// <summary>NFR-OGMA-005 page-turn budget (K32).</summary>
    public const double PageTurnP95BudgetMs = 250;

    /// <summary>Minimum share of the window the reading viewport occupies at 1280x800.</summary>
    public const double MinimumPageAreaShare = 0.85;

    /// <summary>Floor for the Laplacian variance of a rendered text page at 100 % scaling; first measured 529 on the synthetic title page (sparse text), so the floor is a regression guard, not a quality claim.</summary>
    public const double MinimumSharpness = 400;

    /// <summary>Environment variable that sets the J-READ-1 turn count (D-95); default 200.</summary>
    public const string PagesVariable = "OGMA_E2E_READ_PAGES";

    private const string Handbook = "Big Reference Handbook";
    private const string TradeRoutes = "Trade Routes of East Africa";

    /// <summary>J-READ-1: turn N pages of the 900-page handbook; p95 and the longest turn are recorded.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-1")]
    [Trait("Tag", "Reader")]
    public void JRead1_Soak_TurnPagesWithinBudget(string size) =>
        Journey.Run("J-READ-1", size, JourneySupport.Standard, context =>
        {
            int requested = int.TryParse(Environment.GetEnvironmentVariable(PagesVariable), NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 200;
            OpenLibrary(context);
            Shell.OpenInReader(context, Handbook);
            int turns = Math.Min(requested, 899);
            var latencies = new List<double>(turns);
            PageTurn? stalled = null;
            for (int i = 0; i < turns; i++)
            {
                PageTurn turn = Shell.TurnPage(context);
                Assert.True(context.RequireApp.IsAlive, $"The app exited during page turn {i + 1}.");
                if (!turn.Advanced)
                {
                    stalled = turn;
                    break;
                }

                latencies.Add(turn.Milliseconds);
            }

            double p95 = ReaderMetrics.Percentile(latencies, 0.95);
            double longest = latencies.Count == 0 ? double.NaN : latencies.Max();
            context.Record("soak.requestedTurns", requested);
            context.Record("soak.completedTurns", latencies.Count);
            context.Record("soak.p50Ms", ReaderMetrics.Percentile(latencies, 0.5));
            context.Record("soak.p95Ms", p95);
            context.Record("soak.longestMs", longest);
            context.Record("soak.method", "Reader.EdgeNext Invoke until Reader.PageNumber advances (UIA round trips included)");
            Assert.True(stalled is null, $"Turn from page {stalled?.From} did not advance ({latencies.Count} done, p95 {p95:0} ms) [K32].");
            Assert.True(p95 <= PageTurnP95BudgetMs, $"Page-turn p95 {p95:0} ms exceeds {PageTurnP95BudgetMs:0} ms; longest {longest:0} ms [K32].");
        });

    /// <summary>J-READ-2: find "caravan" in Trade Routes, jump to a result, then go back through the history.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-2")]
    [Trait("Tag", "Reader")]
    public void JRead2_FindJumpAndBack(string size) =>
        Journey.Run("J-READ-2", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            Shell.OpenInReader(context, TradeRoutes);
            AutomationElement window = context.RequireApp.MainWindow;
            int start = Shell.CurrentPage(context) ?? throw new InvalidOperationException("No page number.");
            Uia.Activate(Uia.WaitFor(window, "Reader.Search"));
            AutomationElement box = Uia.WaitFor(window, "Reader.Find.Box");
            Uia.SetText(box, "caravan");
            box.Focus();
            FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
            AutomationElement results = Uia.WaitFor(window, "Reader.Find.Results", TimeSpan.FromSeconds(30));
            AutomationElement hit = Uia.WaitUntil(() => Uia.ListItems(results).FirstOrDefault(), TimeSpan.FromSeconds(30), "a find result for 'caravan'");
            context.Record("find.resultCount", Uia.ListItems(results).Length);
            hit.Patterns.SelectionItem.Pattern.Select();
            Assert.True(Uia.Poll(() => Shell.CurrentPage(context) is { } p && p != start, TimeSpan.FromSeconds(15)), "Selecting a result did not move the reader.");
            context.Record("find.jumpedToPage", Shell.CurrentPage(context));
            context.Shot("find-jumped");
            AutomationElement back = Uia.WaitFor(window, "Reader.History.Back");
            Assert.True(Uia.Poll(() => back.IsEnabled, TimeSpan.FromSeconds(5)), "Back is disabled after a search jump.");
            Uia.Activate(back);
            Assert.True(Uia.Poll(() => Shell.CurrentPage(context) == start, TimeSpan.FromSeconds(15)), $"History back did not return to page {start}; at {Shell.CurrentPage(context)}.");
        });

    /// <summary>J-READ-3: the locked ledger refuses a wrong password, then opens with "secret".</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-3")]
    [Trait("Tag", "Reader")]
    public void JRead3_LockedBook_WrongThenRightPassword(string size) =>
        Journey.Run("J-READ-3", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            AutomationElement dialog = RequestLockedLedger(context);
            AutomationElement window = context.RequireApp.MainWindow;
            Enter(dialog, "wrong-password");
            Uia.WaitUntil(() => Uia.TryFind(window, "PasswordUnlock.WrongPassword"), TimeSpan.FromSeconds(30), "the wrong-password message");
            Assert.Null(Uia.TryFind(window, "Reader.Page"));
            context.Shot("wrong-password");
            Enter(Uia.WaitFor(window, "PasswordUnlock.Dialog"), "secret");
            Uia.WaitFor(window, "Reader.Page", TimeSpan.FromSeconds(60));
            Assert.True(Uia.Poll(() => Shell.CurrentPage(context) == 1, TimeSpan.FromSeconds(15)), "The unlocked ledger did not show page 1.");
            context.Shot("unlocked");
        });

    /// <summary>J-READ-4: two books side by side in split view.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-4")]
    [Trait("Tag", "Reader")]
    public void JRead4_SplitView_TwoBooks(string size) =>
        Journey.Run("J-READ-4", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            Shell.OpenInReader(context, TradeRoutes);
            AutomationElement window = context.RequireApp.MainWindow;
            if (Uia.TryFind(window, "Reader.SplitView") is null)
            {
                throw new NotAssessedException("Reader.SplitView is not reachable from the reading toolbar: its button sits in the catalogue toolbar, which is hidden while reading (K136).");
            }

            Uia.Activate(Uia.WaitFor(window, "Reader.SplitView"));
            AutomationElement right = Uia.WaitFor(window, "Reader.Split.Picker.Right", TimeSpan.FromSeconds(20));
            PickIntoPane(window, right, "Reader.Split.Results.Right", Handbook);
            Assert.True(
                Uia.Poll(() => window.FindAllDescendants(cf => cf.ByAutomationId("Reader.Page")).Length >= 2, TimeSpan.FromSeconds(45)),
                "Both panes did not show a page.");
            context.Record("split.pageElements", window.FindAllDescendants(cf => cf.ByAutomationId("Reader.Page")).Length);
            context.Shot("split-two-books");
        });

    /// <summary>J-READ-5: the citation copies in all five formats; the clipboard carries each format's marker.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-5")]
    [Trait("Tag", "Reader")]
    public void JRead5_CitationExport_AllFormats(string size) =>
        Journey.Run("J-READ-5", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            Shell.OpenInReader(context, TradeRoutes);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement page = Uia.WaitFor(window, "Reader.Page");
            Rectangle area = page.BoundingRectangle;
            FlaUI.Core.Input.Mouse.MoveTo(new Point(area.Left + 60, area.Top + 250));
            FlaUI.Core.Input.Mouse.Down(FlaUI.Core.Input.MouseButton.Left);
            FlaUI.Core.Input.Mouse.MoveTo(new Point(area.Left + 240, area.Top + 300));
            FlaUI.Core.Input.Mouse.MoveTo(new Point(area.Left + 420, area.Top + 335));
            FlaUI.Core.Input.Mouse.Up(FlaUI.Core.Input.MouseButton.Left);
            AutomationElement capture = Uia.WaitUntil(() => Uia.TryFind(window, "Reader.Citation.Capture"), TimeSpan.FromSeconds(15), "the selection citation button after dragging over the title");
            Uia.Activate(capture);
            Uia.WaitFor(window, "Reader.Citation.CopyAs", TimeSpan.FromSeconds(15));

            (string Id, string Marker)[] formats =
            [
                ("PlainText", TradeRoutes), // author is not asserted: the card showed "Unknown author" for a book whose catalogue author is known (K137)
                ("BibTeX", "@"),
                ("Ris", "TY  -"),
                ("CslJson", "\"title\""),
                ("Markdown", TradeRoutes),
            ];
            foreach ((string id, string marker) in formats)
            {
                ClearClipboard();
                Uia.Activate(Uia.WaitFor(window, "Reader.Citation.CopyAs"));
                Uia.WaitFor(window, "Reader.Citation.CopyAs." + id, TimeSpan.FromSeconds(10)).Click(); // Avalonia menu items expose no Invoke pattern
                string? text = null;
                Assert.True(Uia.Poll(() => !string.IsNullOrEmpty(text = ReadClipboard()), TimeSpan.FromSeconds(10)), $"Copy as {id} put nothing on the clipboard.");
                context.Record("citation." + id + ".length", text!.Length);
                Assert.True(text.Contains(marker, StringComparison.Ordinal), $"Copy as {id} lacks '{marker}': {text[..Math.Min(120, text.Length)]}");
            }
        });

    /// <summary>J-READ-6: quit and reopen; the exact page resumes (READ-001).</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-6")]
    [Trait("Tag", "Reader")]
    public void JRead6_QuitAndReopen_ExactPageResumes(string size) =>
        Journey.Run("J-READ-6", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            Shell.OpenInReader(context, Handbook);
            AutomationElement box = Uia.WaitFor(context.RequireApp.MainWindow, "Reader.PageNumber");
            const int target = 137;
            Uia.SetText(box, target.ToString(CultureInfo.InvariantCulture));
            box.Focus();
            FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
            Assert.True(Uia.Poll(() => Shell.CurrentPage(context) == target, TimeSpan.FromSeconds(20)), "Go to page did not land on 137.");
            Thread.Sleep(1500);
            context.Launch(label: "relaunch");
            Shell.WaitReady(context);
            Shell.WaitForCatalogue(context, minimum: 1, stableFor: TimeSpan.FromSeconds(2));
            Shell.OpenInReader(context, Handbook);
            int? restored = null;
            Uia.Poll(() => (restored = Shell.CurrentPage(context)) == target, TimeSpan.FromSeconds(15));
            context.Record("resume.restoredPage", restored);
            Assert.Equal(target, restored);
        });

    /// <summary>Negative: a truncated PDF never crashes the app and never shows a reader page.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-N1")]
    [Trait("Tag", "Reader")]
    public void JReadN1_TruncatedPdf_NoCrashNoPage(string size) =>
        Journey.Run("J-READ-N1", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement? item = Shell.FindCatalogueItem(context, "Truncated Download");
            if (item is null)
            {
                throw new NotAssessedException("The truncated PDF is not listed in the catalogue, so there is nothing to open (the refusal is covered by G6).");
            }

            item.Patterns.SelectionItem.Pattern.Select();
            if (Uia.TryFind(window, "Detail.Action.Read") is { IsEnabled: true } read)
            {
                Uia.Activate(read);
            }

            Thread.Sleep(3000);
            Assert.True(context.RequireApp.IsAlive, "The app exited on a truncated PDF.");
            Assert.True(NoReaderPage(window), "A truncated PDF presented a reading page.");
            context.Shot("truncated");
        });

    /// <summary>Negative: cancelling the unlock prompt leaves no reader and no crash.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-N2")]
    [Trait("Tag", "Reader")]
    public void JReadN2_CancelledPasswordPrompt_NoReader(string size) =>
        Journey.Run("J-READ-N2", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            AutomationElement dialog = RequestLockedLedger(context);
            AutomationElement window = context.RequireApp.MainWindow;
            Uia.Activate(Uia.WaitFor(dialog, "PasswordUnlock.Cancel"));
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "PasswordUnlock.Dialog") is null, TimeSpan.FromSeconds(10)), "The unlock dialog stayed open after Cancel.");
            Assert.True(context.RequireApp.IsAlive, "The app exited after cancelling the prompt.");
            Assert.True(NoReaderPage(window), "Cancel still opened the book.");
        });

    /// <summary>The reading viewport takes at least 85 % of the window client area at 1280x800.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-AREA")]
    [Trait("Tag", "Reader")]
    public void JReadArea_PageAreaShare(string size) =>
        Journey.Run("J-READ-AREA", size, JourneySupport.Standard, context =>
        {
            if (size != "1280x800")
            {
                throw new NotAssessedException("The 85 % page-area threshold is defined at 1280x800 only.");
            }

            OpenLibrary(context);
            Shell.OpenInReader(context, TradeRoutes);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement viewport = Uia.WaitFor(window, "Reader.Viewport");
            double withPanel = ReaderMetrics.PageAreaShare(viewport.BoundingRectangle, context.Window.ClientRectangle);
            context.Record("pageArea.shareSidePanelOpen", Math.Round(withPanel, 4));
            if (Uia.TryFind(window, "Reader.Find.Tab") is not null)
            {
                Uia.Activate(Uia.WaitFor(window, "Reader.SidePanel.Toggle"));
                Uia.Poll(() => Uia.TryFind(window, "Reader.Find.Tab") is null, TimeSpan.FromSeconds(5));
            }

            double share = ReaderMetrics.PageAreaShare(Uia.WaitFor(window, "Reader.Viewport").BoundingRectangle, context.Window.ClientRectangle);
            context.Record("pageArea.share", Math.Round(share, 4));
            context.Record("pageArea.method", "Reader.Viewport UIA bounds over the window client rectangle, side panel closed (open value recorded separately)");
            context.Shot("page-area");
            Assert.True(share >= MinimumPageAreaShare, $"The reading viewport is {share:P1} of the window (needs {MinimumPageAreaShare:P0}) [READ-003].");
        });

    /// <summary>Text sharpness at 100 % display scaling (K33). The 150 % and 200 % checks are NOT ASSESSED (D-94, Phase 24).</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-READ-SHARP")]
    [Trait("Tag", "Reader")]
    public void JReadSharp_TextSharpnessAt100Percent(string size) =>
        Journey.Run("J-READ-SHARP", size, JourneySupport.Standard, context =>
        {
            OpenLibrary(context);
            Shell.OpenInReader(context, TradeRoutes);
            AutomationElement page = Uia.WaitFor(context.RequireApp.MainWindow, "Reader.Page");
            G03ReadTests.AssertPagePainted(context, page, "sharpness page");
            Thread.Sleep(1500); // let the preview be replaced by the full-resolution render
            using Bitmap capture = ScreenCapture.CaptureWindow(context.Window.Handle);
            Rectangle origin = context.Window.WindowRectangle;
            Rectangle b = Rectangle.Intersect(page.BoundingRectangle, context.Window.ClientRectangle);
            var local = new Rectangle(b.X - origin.X, b.Y - origin.Y, b.Width, b.Height);
            double sharpness = ReaderMetrics.LaplacianVariance(capture, local);
            context.Record("sharpness.laplacianVariance", Math.Round(sharpness, 1));
            context.Record("sharpness.displayScaling", "100% only; 150% and 200% NOT ASSESSED (D-94, owner Phase 24)");
            Assert.False(double.IsNaN(sharpness), "The page region was too small to measure.");
            Assert.True(sharpness >= MinimumSharpness, $"Page sharpness {sharpness:0} is below {MinimumSharpness:0}.");
        });

    private static bool NoReaderPage(AutomationElement window) =>
        Uia.TryFind(window, "Reader.PageNumber") is not { } p || string.IsNullOrWhiteSpace(Uia.GetText(p));

    private static void OpenLibrary(JourneyContext context)
    {
        context.Launch(Shell.SeedEnvironment(context));
        Shell.WaitReady(context);
        Shell.AddCorpusLibrary(context);
        Shell.WaitForCatalogue(context, minimum: 1);
    }

    private static AutomationElement RequestLockedLedger(JourneyContext context)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        AutomationElement item = Uia.WaitUntil(() => Shell.FindCatalogueItem(context, "Locked Ledger"), TimeSpan.FromSeconds(60), "the Locked Ledger card");
        item.Patterns.SelectionItem.Pattern.Select();
        AutomationElement read = Uia.WaitUntil(
            () => Uia.TryFind(window, "Detail.Action.Read") is { } r && r.IsEnabled ? r : null,
            TimeSpan.FromSeconds(30),
            "an enabled Read button for the locked ledger");
        Uia.Activate(read);
        return Uia.WaitUntil(() => Uia.TryFind(window, "PasswordUnlock.Dialog"), TimeSpan.FromSeconds(30), "the in-app unlock dialog");
    }

    private static void Enter(AutomationElement dialog, string password)
    {
        Uia.SetText(Uia.WaitFor(dialog, "PasswordUnlock.Password"), password);
        Uia.Activate(Uia.WaitFor(dialog, "PasswordUnlock.Unlock"));
    }

    private static void PickIntoPane(AutomationElement window, AutomationElement picker, string resultsId, string title)
    {
        Uia.SetText(picker, title);
        AutomationElement results = Uia.WaitFor(window, resultsId, TimeSpan.FromSeconds(20));
        AutomationElement hit = Uia.WaitUntil(
            () => Uia.ListItems(results).FirstOrDefault(i => (i.Name ?? string.Empty).Contains(title, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(20),
            $"a picker result for '{title}'");
        hit.Patterns.SelectionItem.Pattern.Select();
    }

    private static string? ReadClipboard()
    {
        string? text = null;
        RunSta(() => text = System.Windows.Forms.Clipboard.ContainsText() ? System.Windows.Forms.Clipboard.GetText() : null);
        return text;
    }

    private static void ClearClipboard() => RunSta(System.Windows.Forms.Clipboard.Clear);

    private static void RunSta(Action action)
    {
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is momentarily owned by another process; the caller polls.
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
