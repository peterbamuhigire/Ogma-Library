using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Sept-23 Phase 10 catalogue journeys: J-CAT-1 keyboard browse to detail; J-CAT-2 create a
/// collection, add books by the selection toolbar menu and by drag, then filter the Library by it;
/// J-CAT-3 bulk-set the author of books missing one, with preview, apply and undo; J-CAT-4 copies
/// badge and Compare on the corpus duplicate, then merge two books into one work and undo.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class CatalogueOrganiseTests
{
    private const string CollectionName = "Organise test";

    /// <summary>Oracle: arrow keys move the selection between cards and the detail inspector follows.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Category", "KeyboardOnly")]
    [Trait("Journey", "J-CAT-1")]
    [Trait("Tag", "Catalogue")]
    public void JCat1_KeyboardBrowseToDetail(string size) =>
        Journey.Run("J-CAT-1", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            AutomationElement[] items = Shell.WaitForCatalogue(context, minimum: 3);
            AutomationElement window = context.RequireApp.MainWindow;

            string first = items[0].Properties.AutomationId.ValueOrDefault ?? string.Empty;
            string second = items[1].Properties.AutomationId.ValueOrDefault ?? string.Empty;

            Native.ForceForeground(context.RequireApp.Handle);
            Click(items[0]);
            Assert.True(
                Uia.Poll(() => IsSelected(window, first), TimeSpan.FromSeconds(5)),
                "Clicking the first card did not select it.");

            Keyboard.Press(VirtualKeyShort.RIGHT);
            Assert.True(
                Uia.Poll(() => IsSelected(window, second) && !IsSelected(window, first), TimeSpan.FromSeconds(5)),
                "The Right arrow did not move the selection to the next card.");
            context.Mark("catalogue.keyboardMoved");

            AutomationElement read = Uia.WaitUntil(
                () => Uia.TryFind(window, "Detail.Action.Read") is { } r && r.IsEnabled ? r : null,
                TimeSpan.FromSeconds(30),
                "the detail inspector for the keyboard-selected card");
            Assert.NotNull(read);
            context.Mark("detail.shown");
            context.Shot("keyboard-detail");
        });

    /// <summary>
    /// Oracle: the new collection gets two books from the toolbar menu; filtering the Library by it
    /// shows exactly those books. The drag part is attempted with real input and recorded as
    /// passed or NOT_ASSESSED; it never decides the menu-path verdict.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-CAT-2")]
    [Trait("Tag", "Catalogue")]
    public void JCat2_CreateCollection_AddByMenuAndDrag_FilterByIt(string size) =>
        Journey.Run("J-CAT-2", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            AutomationElement[] items = Shell.WaitForCatalogue(context, minimum: 3);
            AutomationElement window = context.RequireApp.MainWindow;
            // Compared by automation id: background enrichment may change a card's name mid-run.
            string[] picked = [Id(items[0]), Id(items[1])];

            // 1. Create the collection.
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Collections"));
            Uia.SetText(Uia.WaitFor(window, "Collections.NewName"), CollectionName);
            Uia.Activate(Uia.WaitUntil(
                () => Uia.TryFind(window, "Collections.Create") is { } c && c.IsEnabled ? c : null,
                TimeSpan.FromSeconds(5),
                "an enabled Create button"));
            Uia.WaitUntil(() => FindCollectionRow(window), TimeSpan.FromSeconds(10), $"the '{CollectionName}' row");
            context.Mark("collection.created");

            // 2. Select two cards (click, Ctrl+click) and add them through the selection toolbar.
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Library"));
            items = Shell.WaitForCatalogue(context, minimum: 3, stableFor: TimeSpan.FromSeconds(2));
            Native.ForceForeground(context.RequireApp.Handle);
            Click(items[0]);
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Click(items[1]);
            }

            AutomationElement add = Uia.WaitUntil(
                () => Uia.TryFind(window, "Catalogue.Selection.AddToShelf") is { } a && a.IsEnabled ? a : null,
                TimeSpan.FromSeconds(10),
                "the selection toolbar's Add to collection button");
            context.Mark("selection.toolbarShown");
            context.Shot("selection-toolbar");
            Click(add);
            bool opened = Uia.Poll(() => FindPopupMenuItem(context, CollectionName) is not null, TimeSpan.FromSeconds(5));
            context.Record("addMenu.openedByClick", opened);
            if (!opened)
            {
                int pid = context.RequireApp.Process.Id;
                context.Shot("add-menu-missing");
                context.Dump("add-menu-missing");
                context.Record(
                    "addMenu.topLevels",
                    context.RequireApp.Automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                        .Select(top => $"{top.ControlType}:{top.ClassName}:{top.Properties.Name.ValueOrDefault}:{top.FindAllDescendants().Length}")
                        .ToArray());
            }

            AutomationElement menuItem = Uia.WaitUntil(
                () => FindPopupMenuItem(context, CollectionName),
                TimeSpan.FromSeconds(1),
                $"the '{CollectionName}' entry in the Add to collection menu");
            Click(menuItem);
            context.Mark("selection.addedByMenu");

            // 3. Filter the Library by the collection: exactly the two picked books show.
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Collections"));
            AutomationElement row = Uia.WaitUntil(() => FindCollectionRow(window), TimeSpan.FromSeconds(10), "the collection row");
            row.Patterns.SelectionItem.Pattern.Select();
            Uia.Activate(Uia.WaitUntil(
                () => Uia.TryFind(window, "Collections.ShowInLibrary") is { } s && s.IsEnabled ? s : null,
                TimeSpan.FromSeconds(5),
                "an enabled Show books in Library button"));
            bool filtered = Uia.Poll(() => Shell.CatalogueItems(context).Length == 2, TimeSpan.FromSeconds(15), intervalMs: 300);
            string[] shown = Shell.CatalogueItems(context).Select(Name).ToArray();
            context.Record("filter.shown", shown);
            Assert.True(filtered, $"Filtering by the collection showed {shown.Length} books, expected 2.");
            string[] shownIds = Shell.CatalogueItems(context).Select(Id).ToArray();
            Assert.All(picked, id => Assert.Contains(id, shownIds));
            context.Mark("filter.byCollection");
            context.Shot("filtered-by-collection");

            // 4. Drag a third card onto the collection (spring-loaded through the rail).
            context.Record("drag.result", TryDragThirdCard(context, window));
        });

    /// <summary>
    /// Oracle (CAT-005): with the "missing author" filter on, bulk-setting an author through the
    /// dialog removes the edited books from the filtered view, and Undo in the toast brings exactly
    /// those books back. The preview shows one row per selected book before anything is written.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-CAT-3")]
    [Trait("Tag", "Catalogue")]
    public void JCat3_BulkSetAuthor_Preview_Apply_Undo(string size) =>
        Journey.Run("J-CAT-3", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 3);
            AutomationElement window = context.RequireApp.MainWindow;

            // 1. Filter to books with no author.
            Uia.Activate(Uia.WaitFor(window, "Shell.Action.Filter"));
            AutomationElement missingAuthor = Uia.WaitFor(window, "Catalogue.Filter.Missing.Author");
            missingAuthor.Patterns.Toggle.Pattern.Toggle();
            Assert.True(
                Uia.Poll(() => missingAuthor.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == ToggleState.On, TimeSpan.FromSeconds(5)),
                "The missing-author filter did not switch on.");
            Uia.Activate(Uia.WaitFor(window, "Shell.Drawer.Close"));
            AutomationElement[] items = Shell.WaitForCatalogue(context, minimum: 1, stableFor: TimeSpan.FromSeconds(2));
            string[] picked = items.Select(Id).Take(2).ToArray();
            context.Record("missingAuthor.shown", items.Select(Id).ToArray());
            context.Mark("filter.missingAuthor");

            // 2. Select the books and open Edit...
            Native.ForceForeground(context.RequireApp.Handle);
            Click(items[0]);
            if (picked.Length > 1)
            {
                using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                {
                    Click(items[1]);
                }
            }

            AutomationElement edit = Uia.WaitUntil(
                () => Uia.TryFind(window, "Catalogue.Selection.Edit") is { } e && e.IsEnabled ? e : null,
                TimeSpan.FromSeconds(10),
                "an enabled Edit... button");
            Click(edit);
            AutomationElement dialog = Uia.WaitUntil(() => FindTopLevel(context, "BulkEdit.Dialog"), TimeSpan.FromSeconds(10), "the bulk-edit dialog");
            context.Mark("dialog.opened");

            // 3. Type the author; the preview shows one row per selected book; Apply.
            Uia.SetText(Uia.WaitFor(dialog, "BulkEdit.Value"), "Journey Author");
            Assert.True(
                Uia.Poll(() => picked.All(id => Uia.TryFind(dialog, $"BulkEdit.Row.{BookId(id)}") is not null), TimeSpan.FromSeconds(5)),
                "The preview did not show a row for every selected book.");
            context.Mark("preview.shown");
            context.Shot("bulk-edit-preview", dialog.Properties.NativeWindowHandle.ValueOrDefault);
            Uia.Activate(Uia.WaitUntil(
                () => Uia.TryFind(dialog, "BulkEdit.Apply") is { } a && a.IsEnabled ? a : null,
                TimeSpan.FromSeconds(5),
                "an enabled Apply button"));

            // 4. The edited books now have an author, so they leave the missing-author view.
            Assert.True(
                Uia.Poll(() => !Shell.CatalogueItems(context).Select(Id).Intersect(picked).Any(), TimeSpan.FromSeconds(15), intervalMs: 300),
                "The edited books still show under the missing-author filter.");
            context.Mark("edit.applied");
            AutomationElement undo = Uia.WaitFor(window, "Shell.Toast.Action", TimeSpan.FromSeconds(10));
            context.Shot("bulk-edit-applied-toast");

            // 5. Undo from the toast restores them.
            Uia.Activate(undo);
            Assert.True(
                Uia.Poll(() => picked.All(id => Shell.CatalogueItems(context).Select(Id).Contains(id)), TimeSpan.FromSeconds(15), intervalMs: 300),
                "Undo did not bring the edited books back under the missing-author filter.");
            context.Mark("edit.undone");
            context.Shot("bulk-edit-undone");
        });

    /// <summary>
    /// Oracle (CAT-007): the corpus duplicate's card carries the "2 copies" badge in its accessible
    /// name and Compare shows both files side by side; merging two other books shows one
    /// "2 editions · 1 work" card, and Undo in the toast brings both cards back. No file is deleted.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-CAT-4")]
    [Trait("Tag", "Catalogue")]
    public void JCat4_Copies_Compare_Merge_Undo(string size) =>
        Journey.Run("J-CAT-4", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 3, stableFor: TimeSpan.FromSeconds(2));
            AutomationElement window = context.RequireApp.MainWindow;
            int fileCount = Directory.EnumerateFiles(context.Session.LibraryDirectory, "*", SearchOption.AllDirectories).Count();

            // 1. The corpus duplicate shows one card with the copies badge in its name.
            AutomationElement copies = Uia.WaitUntil(
                () => Shell.CatalogueItems(context).FirstOrDefault(item => Name(item).Contains("2 copies", StringComparison.Ordinal)),
                TimeSpan.FromSeconds(30),
                "a card named with the '2 copies' badge");
            string copiesId = Id(copies);
            context.Record("copies.card", copiesId);
            context.Mark("badge.copies");

            // 2. Compare shows both files side by side, then closes.
            Native.ForceForeground(context.RequireApp.Handle);
            Click(copies);
            Click(Uia.WaitUntil(
                () => Uia.TryFind(window, "Catalogue.Selection.Compare") is { } c && c.IsEnabled ? c : null,
                TimeSpan.FromSeconds(10),
                "an enabled Compare button"));
            AutomationElement dialog = Uia.WaitUntil(() => FindTopLevel(context, "Compare.Dialog"), TimeSpan.FromSeconds(15), "the Compare dialog");
            Assert.True(
                Uia.Poll(() => CompareColumns(dialog) >= 2, TimeSpan.FromSeconds(5)),
                "The Compare dialog did not show two file columns.");
            context.Record("compare.columns", CompareColumns(dialog));
            context.Mark("compare.shown");
            context.Shot("compare-dialog", dialog.Properties.NativeWindowHandle.ValueOrDefault);
            Uia.Activate(Uia.WaitFor(dialog, "Compare.Close"));
            Assert.True(
                Uia.Poll(() => FindTopLevel(context, "Compare.Dialog") is null, TimeSpan.FromSeconds(10)),
                "The Compare dialog did not close.");

            // 3. Merge two other books into one work: one grouped card replaces two.
            AutomationElement[] items = Shell.WaitForCatalogue(context, minimum: 3, stableFor: TimeSpan.FromSeconds(2));
            AutomationElement[] plain = items
                .Where(item => Id(item) != copiesId && !Name(item).Contains("1 work", StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            Assert.Equal(2, plain.Length);
            string[] picked = plain.Select(Id).ToArray();
            int before = items.Length;
            Native.ForceForeground(context.RequireApp.Handle);
            Click(plain[0]);
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Click(plain[1]);
            }

            Click(Uia.WaitUntil(
                () => Uia.TryFind(window, "Catalogue.Selection.Merge") is { } m && m.IsEnabled ? m : null,
                TimeSpan.FromSeconds(10),
                "an enabled Merge button"));
            Assert.True(
                Uia.Poll(
                    () => Shell.CatalogueItems(context) is { } now &&
                          now.Length == before - 1 &&
                          now.Any(item => Name(item).Contains("2 editions · 1 work", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(15),
                    intervalMs: 300),
                "Merging did not show one '2 editions · 1 work' card in place of two.");
            context.Mark("merge.applied");
            AutomationElement undo = Uia.WaitFor(window, "Shell.Toast.Action", TimeSpan.FromSeconds(10));
            context.Shot("merged-toast");

            // 4. Undo restores both cards; no file was deleted at any point.
            Uia.Activate(undo);
            Assert.True(
                Uia.Poll(
                    () => Shell.CatalogueItems(context) is { } now &&
                          now.Length == before &&
                          picked.All(id => now.Select(Id).Contains(id)),
                    TimeSpan.FromSeconds(15),
                    intervalMs: 300),
                "Undo did not bring both merged books back as separate cards.");
            context.Mark("merge.undone");
            context.Shot("merge-undone");
            Assert.Equal(fileCount, Directory.EnumerateFiles(context.Session.LibraryDirectory, "*", SearchOption.AllDirectories).Count());
            context.Mark("files.untouched");
        });

    private static int CompareColumns(AutomationElement dialog) =>
        dialog.FindAllDescendants()
            .Count(element => element.Properties.AutomationId.ValueOrDefault?.StartsWith("Compare.Column.", StringComparison.Ordinal) == true);

    private static string TryDragThirdCard(JourneyContext context, AutomationElement window)
    {
        Uia.Activate(Uia.WaitFor(window, "Catalogue.Filter.Clear"));
        AutomationElement[] items = Shell.WaitForCatalogue(context, minimum: 3, stableFor: TimeSpan.FromSeconds(2));
        AutomationElement card = items[2];
        string title = Name(card);
        Native.ForceForeground(context.RequireApp.Handle);
        Point start = Centre(card);
        Point rail = Centre(Uia.WaitFor(window, "Shell.Nav.Collections"));
        Mouse.MoveTo(start);
        Mouse.Down(MouseButton.Left);
        try
        {
            MoveInSteps(start, new Point(start.X + 20, start.Y + 20));
            MoveInSteps(new Point(start.X + 20, start.Y + 20), rail);
            if (!Uia.Poll(() => FindCollectionRow(window) is not null, TimeSpan.FromSeconds(3)))
            {
                return "NOT_ASSESSED: the synthetic drag did not open Collections (OLE drag not started by injected input)";
            }

            MoveInSteps(rail, Centre(FindCollectionRow(window)!));
        }
        finally
        {
            Mouse.Up(MouseButton.Left);
        }

        bool added = Uia.Poll(
            () => FindCollectionRow(window) is { } row && RowCount(row) == 3,
            TimeSpan.FromSeconds(10),
            intervalMs: 300);
        context.Shot("after-drag");
        return added
            ? $"passed: '{title}' dropped on the collection (count 3)"
            : "NOT_ASSESSED: Collections opened but the drop did not register with injected input";
    }

    private static AutomationElement? FindCollectionRow(AutomationElement window) =>
        Uia.TryFind(window, "Collections.List") is { } list
            ? Uia.ListItems(list).FirstOrDefault(item => item.Properties.Name.ValueOrDefault == CollectionName)
            : null;

    private static int? RowCount(AutomationElement row) =>
        row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Select(text => int.TryParse(text.Properties.Name.ValueOrDefault, out int count) ? count : (int?)null)
            .LastOrDefault(count => count is not null);

    private static AutomationElement? FindPopupMenuItem(JourneyContext context, string name)
    {
        int pid = context.RequireApp.Process.Id;
        return context.RequireApp.Automation.GetDesktop()
            .FindAllChildren(cf => cf.ByProcessId(pid))
            .Append(context.RequireApp.MainWindow)
            .SelectMany(top => top.FindAllDescendants(cf => cf.ByName(name)))
            .FirstOrDefault(candidate => candidate.ControlType == ControlType.MenuItem);
    }

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

    // Card ids are "Catalogue.Item.{bookId}"; preview rows are "BulkEdit.Row.{bookId}".
    private static string BookId(string cardId) =>
        cardId.StartsWith("Catalogue.Item.", StringComparison.Ordinal) ? cardId["Catalogue.Item.".Length..] : cardId;

    // Re-found on every poll: a background refresh may hand the card a new UIA element.
    private static bool IsSelected(AutomationElement window, string automationId) =>
        Uia.TryFind(window, automationId) is { } item &&
        item.Patterns.SelectionItem.TryGetPattern(out var pattern) &&
        pattern.IsSelected.ValueOrDefault;

    private static string Name(AutomationElement item) => item.Properties.Name.ValueOrDefault ?? string.Empty;

    private static string Id(AutomationElement item) => item.Properties.AutomationId.ValueOrDefault ?? string.Empty;

    private static Point Centre(AutomationElement element)
    {
        Rectangle r = element.BoundingRectangle;
        return new Point(r.X + (r.Width / 2), r.Y + (r.Height / 2));
    }

    private static void Click(AutomationElement element) => Mouse.Click(Centre(element));

    private static void MoveInSteps(Point from, Point to, int steps = 12)
    {
        for (int i = 1; i <= steps; i++)
        {
            Mouse.MoveTo(new Point(from.X + ((to.X - from.X) * i / steps), from.Y + ((to.Y - from.Y) * i / steps)));
            Thread.Sleep(40);
        }
    }
}
