using System.Security.Cryptography;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Sept-23 Phase 11 inspector journeys: J-DET-1 select, inspect, Read and back; J-DET-2 accept
/// proposals and undo; J-DET-3 write back and restore on the temporary corpus copy (hashes
/// compared); J-DET-4 missing file, in-panel error and Locate file; the negative Read on a deleted
/// file; and G9 (metadata edit, enrich, preview write-back, undo), which replaces the Phase 01
/// placeholder.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class DetailTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    // The state card is a Border, which UIA does not expose; its body text carries the id instead.
    private const string MissingStateId = "Detail.State.MissingFile.Body";

    /// <summary>J-DET-1: select → inspect → primary action → reader → back to the catalogue.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-DET-1")]
    [Trait("Tag", "Detail")]
    public void JDet1_InspectReadAndReturn(string size) =>
        Journey.Run("J-DET-1", size, JourneySupport.Standard, context =>
        {
            string title = Corpus.Oracle.Reader.Title;
            AutomationElement panel = OpenLibraryAndInspect(context, title);
            AutomationElement window = context.RequireApp.MainWindow;
            Visibility.AssertVisiblyPainted(context.Window, panel, "Detail inspector");
            Assert.Contains(title, Uia.GetText(Uia.WaitFor(panel, "Detail.Title")) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            context.Shot("inspector-open");

            Uia.Activate(WaitEnabled(window, "Detail.Action.Read"));
            Uia.WaitUntil(
                () => Uia.TryFind(window, "Reader.PageNumber") is { } p && !string.IsNullOrWhiteSpace(Uia.GetText(p)) ? p : null,
                TimeSpan.FromSeconds(60),
                "the reader page number");
            context.Mark("reader.opened");

            Uia.Activate(Uia.WaitFor(window, "Reader.BackToLibrary"));
            AutomationElement grid = Uia.WaitFor(window, "Catalogue.Grid");
            Visibility.AssertVisiblyPainted(context.Window, grid, "Catalogue grid after returning from the reader");
            context.Mark("library.returned");
        });

    /// <summary>
    /// J-DET-2: accept proposals and undo. Proposals come only from an online metadata provider;
    /// the harness has no offline provider seam yet (the mock lands with Phases 15-16), so the
    /// journey reports NOT ASSESSED instead of passing on an empty review card (D-81).
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-DET-2")]
    [Trait("Tag", "Detail")]
    public void JDet2_AcceptProposalsAndUndo(string size) =>
        Journey.Run("J-DET-2", size, JourneySupport.Standard, _ =>
            throw new NotAssessedException(
                "No offline metadata-provider seam: proposals cannot be seeded without a live provider (D-81)."));

    /// <summary>J-DET-3: edit, write back, then restore on the temporary corpus copy; hashes compared.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-DET-3")]
    [Trait("Tag", "Detail")]
    public void JDet3_WriteBackAndRestore(string size) =>
        Journey.Run("J-DET-3", size, JourneySupport.Standard, context =>
        {
            CorpusBook book = PickBook(1);
            AutomationElement panel = OpenLibraryAndInspect(context, book.Title!);
            string path = Path.Combine(context.Session.LibraryDirectory, book.File);
            string original = Sha256(path);
            context.Record("writeBack.originalSha256", original);

            EditField(context, panel, "Title", book.Title + " (E2E)");
            OpenMoreAction(context, "Detail.Action.WriteMetadata");
            AutomationElement dialog = Uia.WaitUntil(() => FindTopLevel(context, "WriteBack.Dialog"), Wait, "the write-back dialog");
            context.Shot("writeback-review");
            Uia.Activate(WaitEnabled(dialog, "WriteBack.Continue"));
            Uia.Activate(WaitEnabled(dialog, "WriteBack.Confirm"));
            Uia.Activate(WaitEnabled(dialog, "WriteBack.Close"));
            context.Mark("writeBack.written");

            bool changed = Uia.Poll(() => TrySha256(path) is { } now && now != original, Wait);
            context.Record("writeBack.writtenSha256", TrySha256(path));
            Assert.True(changed, "The PDF on the corpus copy is unchanged after write-back.");

            Uia.Activate(WaitEnabled(context.RequireApp.MainWindow, "Detail.WriteBack.Restore"));
            ConfirmRestoreIfAsked(context);
            bool restored = Uia.Poll(() => TrySha256(path) == original, Wait);
            context.Record("writeBack.restoredSha256", TrySha256(path));
            context.Record("writeBack.status", Uia.TryFind(context.RequireApp.MainWindow, "Detail.WriteBack.Status") is { } s ? Uia.GetText(s) : null);
            Assert.True(restored, "Restore did not bring back the original bytes (SHA-256 differs).");
            context.Mark("writeBack.restored");
        });

    /// <summary>J-DET-4: delete the file → Read → in-panel missing state → Locate file → relinked.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-DET-4")]
    [Trait("Tag", "Detail")]
    public void JDet4_MissingFileLocateAndRelink(string size) =>
        Journey.Run("J-DET-4", size, JourneySupport.Standard, context =>
        {
            CorpusBook book = PickBook(2);
            OpenLibraryAndInspect(context, book.Title!);
            string path = Path.Combine(context.Session.LibraryDirectory, book.File);
            string parked = Path.Combine(context.Session.DataDirectory, "parked-" + Path.GetFileName(book.File));
            File.Move(path, parked);

            ReadAndExpectMissing(context);

            // Put the same bytes back under a new name inside the enabled root, then locate it.
            string relocated = Path.Combine(Path.GetDirectoryName(path)!, "relinked-" + Path.GetFileName(book.File));
            File.Move(parked, relocated);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement locate = WaitEnabled(window, "Detail.Action.Read");
            context.Record("locate.label", locate.Properties.Name.ValueOrDefault);
            Uia.Activate(locate);
            context.Record("locate.technique", FolderPicker.Pick(context.RequireApp, relocated));

            bool relinked = Uia.Poll(() => Uia.TryFind(window, MissingStateId) is null, Wait);
            context.Record("relink.status", Uia.TryFind(window, "Detail.RelinkStatus") is { } s ? Uia.GetText(s) : null);
            context.Shot("relinked");
            Assert.True(relinked, "The missing-file state is still shown after Locate file picked the moved copy.");
            context.Mark("relink.done");
        });

    /// <summary>Negative: Read on a deleted file keeps the shell on the catalogue with an in-panel error.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-DET-Negative")]
    [Trait("Tag", "Detail")]
    public void JDetNegative_ReadOnDeletedFileStaysOnCatalogue(string size) =>
        Journey.Run("J-DET-Negative", size, JourneySupport.Standard, context =>
        {
            CorpusBook book = PickBook(3);
            OpenLibraryAndInspect(context, book.Title!);
            File.Delete(Path.Combine(context.Session.LibraryDirectory, book.File));
            ReadAndExpectMissing(context);
        });

    /// <summary>G9: metadata edit, enrich, preview write-back (cancelled), undo the edit.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G9")]
    [Trait("Tag", "Detail")]
    public void G9_Metadata_EditEnrichPreviewUndo(string size) =>
        Journey.Run("G9", size, JourneySupport.Standard, context =>
        {
            CorpusBook book = PickBook(1);
            AutomationElement panel = OpenLibraryAndInspect(context, book.Title!);
            string path = Path.Combine(context.Session.LibraryDirectory, book.File);
            string original = Sha256(path);
            string edited = book.Title + " (G9)";

            EditField(context, panel, "Title", edited);
            Assert.Contains("(G9)", Uia.GetText(Uia.WaitFor(panel, "Detail.Title")) ?? string.Empty, StringComparison.Ordinal);

            // Enrich: providers are off in the seeded environment, so only an honest state is asserted.
            AutomationElement? enrich = OpenMoreAction(context, "Detail.Action.Enrich", invoke: false);
            context.Record("enrich.enabled", enrich?.IsEnabled);
            if (enrich is { IsEnabled: true })
            {
                ClickMenuItem(context, enrich);
                Thread.Sleep(1500);
            }
            else
            {
                CloseMenu(context);
            }

            Assert.True(context.RequireApp.IsAlive, "The app exited after Enrich.");
            context.Shot("after-enrich");

            OpenMoreAction(context, "Detail.Action.WriteMetadata");
            AutomationElement dialog = Uia.WaitUntil(() => FindTopLevel(context, "WriteBack.Dialog"), Wait, "the write-back dialog");
            Uia.WaitFor(dialog, "WriteBack.Diff");
            context.Shot("writeback-preview");
            Uia.Activate(WaitEnabled(dialog, "WriteBack.Cancel"));
            Assert.True(Uia.Poll(() => FindTopLevel(context, "WriteBack.Dialog") is null, Wait), "The write-back dialog did not close on Cancel.");
            Assert.Equal(original, Sha256(path));

            Uia.Activate(WaitEnabled(context.RequireApp.MainWindow, "Detail.Action.UndoFieldEdit"));
            bool undone = Uia.Poll(
                () => Uia.TryFind(panel, "Detail.Title") is { } t && !(Uia.GetText(t) ?? string.Empty).Contains("(G9)", StringComparison.Ordinal),
                Wait);
            Assert.True(undone, "Undo did not restore the original title.");
            context.Mark("g9.undone");
        });

    internal static AutomationElement OpenLibraryAndInspect(JourneyContext context, string title)
    {
        context.Launch(Shell.SeedEnvironment(context));
        Shell.WaitReady(context);
        Shell.AddCorpusLibrary(context);
        Shell.WaitForCatalogue(context, minimum: 1);
        AutomationElement item = Uia.WaitUntil(() => Shell.FindCatalogueItem(context, title), TimeSpan.FromSeconds(60), $"catalogue item '{title}'");
        item.Patterns.SelectionItem.Pattern.Select();
        AutomationElement window = context.RequireApp.MainWindow;
        AutomationElement? panel = Uia.Poll(() => Uia.TryFind(window, "Detail.Panel") is not null, TimeSpan.FromSeconds(15))
            ? Uia.TryFind(window, "Detail.Panel")
            : null;
        context.Record("detail.healthViewShown", Uia.TryFind(window, "Health.Loaded") is not null);
        Assert.True(panel is not null, $"Selecting '{title}' did not open the detail inspector.");
        Uia.WaitFor(panel!, "Detail.Title");
        context.Mark("detail.open");
        return panel!;
    }

    private static void ReadAndExpectMissing(JourneyContext context)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        Uia.Activate(WaitEnabled(window, "Detail.Action.Read"));
        Uia.WaitFor(window, MissingStateId);
        context.Shot("missing-file");
        Assert.Null(Uia.TryFind(window, "Reader.PageNumber"));
        AutomationElement grid = Uia.WaitFor(window, "Catalogue.Grid");
        Assert.False(grid.IsOffscreen, "The catalogue grid is off screen after Read on a missing file.");
        Visibility.AssertVisiblyPainted(context.Window, grid, "Catalogue grid after Read on a missing file");

        context.Mark("missing.shown");
    }

    private static void EditField(JourneyContext context, AutomationElement panel, string field, string value)
    {
        Uia.Activate(Uia.WaitFor(panel, "Detail.Section.Details"));
        Uia.Activate(Uia.WaitFor(panel, $"Detail.Field.{field}"));
        AutomationElement editor = WaitEnabled(panel, $"Detail.Field.{field}.Editor");
        Uia.SetText(editor, value);
        Native.ForceForeground(context.RequireApp.Handle);
        editor.Focus();
        Keyboard.Press(VirtualKeyShort.RETURN);
        Uia.WaitUntil(
            () => Uia.TryFind(panel, "Detail.Action.UndoFieldEdit") is { IsOffscreen: false } u ? u : null,
            Wait,
            $"the field-edit undo after editing {field}");
        context.Mark($"field.{field}.edited");
    }

    private static AutomationElement? OpenMoreAction(JourneyContext context, string automationId, bool invoke = true)
    {
        Uia.Activate(Uia.WaitFor(context.RequireApp.MainWindow, "Detail.Action.More"));
        AutomationElement item = Uia.WaitUntil(() => FindTopLevel(context, automationId), Wait, $"menu item {automationId}");
        if (invoke)
        {
            ClickMenuItem(context, item);
        }

        return item;
    }

    // Avalonia menu items expose no Invoke pattern, so they are clicked like a user would.
    private static void ClickMenuItem(JourneyContext context, AutomationElement item)
    {
        Native.ForceForeground(context.RequireApp.Handle);
        Thread.Sleep(200);
        item.Click();
    }

    private static void CloseMenu(JourneyContext context)
    {
        Native.ForceForeground(context.RequireApp.Handle);
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(300);
    }

    private static void ConfirmRestoreIfAsked(JourneyContext context)
    {
        if (Uia.Poll(() => FindTopLevel(context, "WriteBack.Restore") is { IsEnabled: true }, TimeSpan.FromSeconds(3)) &&
            FindTopLevel(context, "WriteBack.Restore") is { } confirm)
        {
            Uia.Activate(confirm);
            context.Mark("restore.confirmedInDialog");
        }
    }

    private static AutomationElement WaitEnabled(AutomationElement root, string automationId) =>
        Uia.WaitUntil(() => Uia.TryFind(root, automationId) is { IsEnabled: true } e ? e : null, Wait, $"an enabled {automationId}");

    private static AutomationElement? FindTopLevel(JourneyContext context, string automationId)
    {
        int pid = context.RequireApp.Process.Id;
        return context.RequireApp.Automation.GetDesktop()
            .FindAllChildren(cf => cf.ByProcessId(pid))
            .Append(context.RequireApp.MainWindow)
            .SelectMany(top => top.Properties.AutomationId.ValueOrDefault == automationId
                ? [top]
                : top.FindAllDescendants(cf => cf.ByAutomationId(automationId)))
            .FirstOrDefault();
    }

    // Distinct titled books per journey, skipping the reader probe so G3/J-DET-1 stay independent.
    private static CorpusBook PickBook(int index) =>
        Corpus.Oracle.Catalogue
            .Where(b => !string.IsNullOrWhiteSpace(b.Title) && b.Title != Corpus.Oracle.Reader.Title)
            .ElementAt(index);

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string? TrySha256(string path)
    {
        try
        {
            return File.Exists(path) ? Sha256(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
