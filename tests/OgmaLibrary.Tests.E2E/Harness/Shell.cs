using System.Diagnostics;
using FlaUI.Core.AutomationElements;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>How a journey chose the library folder.</summary>
public enum PickerRoute
{
    /// <summary>The OGMA_E2E_PICK_FOLDER hook (E2E builds only); everything after the picker is production code.</summary>
    Hook,

    /// <summary>The real native folder dialog, driven by the harness.</summary>
    Dialog,
}

/// <summary>Page-object helpers for the shell, catalogue, detail and reader (AutomationId-first).</summary>
public static class Shell
{
    /// <summary>The first-run / idle shell is ready: the empty state or the catalogue is present.</summary>
    public static void WaitReady(JourneyContext context, TimeSpan? timeout = null)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        Uia.WaitUntil(
            () => Uia.TryFind(window, "Catalogue.EmptyState") ?? Uia.TryFind(window, "Catalogue.Grid") ?? Uia.TryFind(window, "Shell.Degraded"),
            timeout ?? TimeSpan.FromSeconds(60),
            "the library shell (empty state, catalogue grid or degraded panel)");
        context.Mark("shell.ready");
    }

    /// <summary>Launch environment that seeds the library folder through the hook when allowed.</summary>
    public static Dictionary<string, string> SeedEnvironment(JourneyContext context, bool allowHook = true) =>
        allowHook && !string.Equals(E2ESettings.SeedMode, "dialog", StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, string> { ["OGMA_E2E_PICK_FOLDER"] = context.Session.LibraryDirectory }
            : [];

    /// <summary>
    /// Copies the corpus into the session library folder, presses Choose folder and completes the
    /// picker: through the hook when the build has it (no dialog appears), otherwise the real dialog.
    /// </summary>
    public static PickerRoute AddCorpusLibrary(JourneyContext context, string buttonId = "Shell.Action.ChooseFolder")
    {
        Corpus.CopyTo(context.Session.LibraryDirectory);
        OgmaApp app = context.RequireApp;
        AutomationElement button = Uia.WaitFor(app.MainWindow, buttonId);
        Uia.Activate(button);
        context.Mark("library.chooseFolderPressed");
        // Without the hook the real dialog must appear (it can take several seconds on a busy first
        // run); with the hook, a dialog only appears when the build lacks OGMA_E2E.
        bool dialogOpened = !context.PickerHookRequested ||
                            Uia.Poll(() => FolderPicker.TryFindDialog(app) is not null, TimeSpan.FromSeconds(3));
        PickerRoute route;
        if (dialogOpened)
        {
            string technique = FolderPicker.Pick(app, context.Session.LibraryDirectory);
            context.Record("library.pickerTechnique", technique);
            route = PickerRoute.Dialog;
        }
        else
        {
            route = PickerRoute.Hook;
        }

        context.Record("library.pickerRoute", route.ToString());
        context.Mark("library.folderChosen");
        return route;
    }

    /// <summary>Catalogue items currently in the grid.</summary>
    public static AutomationElement[] CatalogueItems(JourneyContext context)
    {
        AutomationElement? grid = Uia.TryFind(context.RequireApp.MainWindow, "Catalogue.Grid");
        return grid is null ? [] : Uia.ListItems(grid);
    }

    /// <summary>Waits until the grid shows at least <paramref name="minimum"/> items and the count is stable.</summary>
    public static AutomationElement[] WaitForCatalogue(JourneyContext context, int minimum, TimeSpan? timeout = null, TimeSpan? stableFor = null)
    {
        TimeSpan limit = timeout ?? TimeSpan.FromMinutes(3);
        TimeSpan stable = stableFor ?? TimeSpan.FromSeconds(8);
        var clock = Stopwatch.StartNew();
        int lastCount = -1;
        var lastChange = Stopwatch.StartNew();
        AutomationElement[] items = [];
        while (clock.Elapsed < limit)
        {
            try
            {
                items = CatalogueItems(context);
            }
            catch (Exception ex) when (Uia.IsTransient(ex))
            {
                items = [];
            }

            if (items.Length != lastCount)
            {
                lastCount = items.Length;
                lastChange.Restart();
                if (items.Length > 0 && !context.HasMark("catalogue.firstItem"))
                {
                    context.Mark("catalogue.firstItem");
                }
            }

            if (items.Length >= minimum && lastChange.Elapsed >= stable)
            {
                context.Mark("catalogue.settled");
                context.Record("catalogue.count", items.Length);
                return items;
            }

            Thread.Sleep(500);
        }

        context.Record("catalogue.count", items.Length);
        throw new TimeoutException($"The catalogue did not settle with at least {minimum} items within {limit.TotalSeconds:0} s (last count {lastCount}).");
    }

    /// <summary>Finds a catalogue item whose accessible name or descendant text contains <paramref name="title"/>.</summary>
    public static AutomationElement? FindCatalogueItem(JourneyContext context, string title)
    {
        foreach (AutomationElement item in CatalogueItems(context))
        {
            if (Contains(item.Properties.Name.ValueOrDefault, title) ||
                item.FindFirstDescendant(cf => cf.ByName(title)) is not null)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Selects a book, waits for the detail inspector, presses Read and waits for page 1.</summary>
    public static AutomationElement OpenInReader(JourneyContext context, string title)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        AutomationElement item = Uia.WaitUntil(() => FindCatalogueItem(context, title), TimeSpan.FromSeconds(60), $"catalogue item '{title}'");
        item.Patterns.SelectionItem.Pattern.Select();
        AutomationElement read = Uia.WaitUntil(
            () => Uia.TryFind(window, "Detail.Action.Read") is { } r && r.IsEnabled ? r : null,
            TimeSpan.FromSeconds(30),
            "an enabled Read button in the detail inspector");
        context.Mark("detail.readEnabled");
        Uia.Activate(read);
        AutomationElement page = Uia.WaitUntil(
            () => Uia.TryFind(window, "Reader.PageNumber") is { } p && !string.IsNullOrWhiteSpace(Uia.GetText(p)) ? p : null,
            TimeSpan.FromSeconds(60),
            "the reader page number");
        context.Mark("reader.opened");
        return page;
    }

    /// <summary>
    /// Presses Next (<c>Reader.Next</c>) and waits for the page number to advance. A press the
    /// reader ignores (its command is still busy) is repeated every 2 s and counted, so dropped
    /// clicks show up in the evidence instead of hiding in a long timeout.
    /// </summary>
    public static PageTurn TurnPage(JourneyContext context, TimeSpan? timeout = null)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        AutomationElement next = Uia.WaitUntil(
            () => Uia.TryFind(window, "Reader.Next") is { } n && n.IsEnabled ? n : null,
            TimeSpan.FromSeconds(10),
            "an enabled Next page button");
        int before = CurrentPage(context) ?? throw new InvalidOperationException("The reader shows no page number.");
        var clock = Stopwatch.StartNew();
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(10);
        int presses = 0;
        while (clock.Elapsed < limit)
        {
            Uia.Activate(next);
            presses++;
            if (Uia.Poll(() => CurrentPage(context) == before + 1, TimeSpan.FromSeconds(2), intervalMs: 10))
            {
                return new PageTurn(before, before + 1, clock.Elapsed.TotalMilliseconds, presses - 1, true);
            }
        }

        return new PageTurn(before, CurrentPage(context) ?? before, clock.Elapsed.TotalMilliseconds, presses - 1, false);
    }

    /// <summary>The reader's current page number, or null.</summary>
    public static int? CurrentPage(JourneyContext context)
    {
        AutomationElement? box = Uia.TryFind(context.RequireApp.MainWindow, "Reader.PageNumber");
        return box is not null && int.TryParse(Uia.GetText(box), out int page) ? page : null;
    }

    /// <summary>
    /// Whether a catalogue card shows a real cover image: its cover view exists and does not
    /// contain the placeholder label (the placeholder is an accent block with the title text).
    /// </summary>
    public static bool HasCoverImage(AutomationElement item)
    {
        AutomationElement? cover = item.FindFirstDescendant(cf => cf.ByAutomationId("Catalogue.Item.Cover"));
        return cover is not null && cover.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)) is null;
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One measured page turn.</summary>
public sealed record PageTurn(int From, int To, double Milliseconds, int IgnoredPresses, bool Advanced);
