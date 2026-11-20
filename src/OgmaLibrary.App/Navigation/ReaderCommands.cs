using Avalonia.Controls;
using Avalonia.Input;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.Navigation;

/// <summary>
/// The reader's actions as <see cref="ShellCommand"/>s (Sept-23 Phase 12, READ-013, UX-005). Every
/// reader action lives in the one registry, so the <c>?</c> sheet and the command palette list it and
/// the duplicate-gesture test covers it. <c>Primary</c> gestures are Ctrl on Windows and Cmd on macOS.
/// </summary>
/// <remarks>
/// Two gestures are shared with a shell command by context, not duplicated: Ctrl+F is
/// <c>nav.search</c> in the library and the book's find bar while reading, and Alt+Left/Right is
/// <c>nav.back</c>/<c>nav.forward</c> in the library and page history while reading. In both cases
/// the shell already offers the gesture to the open book first (<c>TryExecuteGesture</c>), so the
/// reader entries below carry no gesture of their own for those two. The Side panel shortcut is F4
/// because Ctrl+B already toggles the bookmark.
/// </remarks>
public static class ReaderCommands
{
    /// <summary>The id of the side-panel toggle command.</summary>
    public const string SidePanelId = "reader.side-panel";

    /// <summary>
    /// Maps each reader toolbar control (by AutomationId) to what makes it reachable from the
    /// keyboard: <c>cmd:&lt;id&gt;</c> is a registered command that carries a gesture (or, for the
    /// two contextual gestures above, the shell command that owns it).
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToolbarShortcuts { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Reader.BackToLibrary"] = "cmd:nav.library",
            ["Reader.History.Back"] = "cmd:reader.history-back",
            ["Reader.History.Forward"] = "cmd:reader.history-forward",
            ["Reader.PageNumber"] = "cmd:reader.go-to-page",
            ["Reader.Zoom"] = "cmd:reader.zoom-in",
            ["Reader.Layout"] = "cmd:reader.layout-cycle",
            ["Reader.Search"] = "cmd:nav.search",
            ["Reader.Annotate"] = "cmd:reader.highlight",
            ["Reader.More"] = "access:Alt+M",
            ["Reader.SidePanel.Toggle"] = "cmd:" + SidePanelId,
        };

    /// <summary>Adds every reader command to <paramref name="registry"/>.</summary>
    /// <param name="registry">The shell registry.</param>
    /// <param name="reader">Gets the main reader view model, or null when there is none.</param>
    /// <param name="isReading">Whether the reader is the visible content.</param>
    /// <returns>The same registry.</returns>
    public static CommandRegistry Register(
        CommandRegistry registry,
        Func<ReaderViewModel?> reader,
        Func<bool> isReading)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(isReading);

        static CommandGesture Primary(Key key, bool shift = false) => new(key, Primary: true, Shift: shift);

        bool Open() => isReading() && reader() is { IsOpen: true };
        Func<bool> when(Func<ReaderViewModel, bool> condition) =>
            () => Open() && condition(reader()!);
        Func<TopLevel?, Task> run(Func<ReaderViewModel, Task> action) =>
            _ => reader() is { } vm ? action(vm) : Task.CompletedTask;
        Func<TopLevel?, Task> now(Action<ReaderViewModel> action) =>
            _ =>
            {
                if (reader() is { } vm)
                {
                    action(vm);
                }

                return Task.CompletedTask;
            };

        ShellCommand Add(string id, Func<TopLevel?, Task> execute, Func<bool> available, params CommandGesture[] gestures) =>
            new("reader." + id, "Command.Reader." + id, CommandGroup.Reader, execute, available, gestures);

        registry
            .Add(Add("next-page", run(vm => vm.GoNextAsync()), Open,
                new CommandGesture(Key.PageDown), new CommandGesture(Key.Right), new CommandGesture(Key.Space)))
            .Add(Add("previous-page", run(vm => vm.GoPreviousAsync()), Open,
                new CommandGesture(Key.PageUp), new CommandGesture(Key.Left)))
            .Add(Add("first-page", run(vm => vm.GoFirstAsync()), Open, new CommandGesture(Key.Home)))
            .Add(Add("last-page", run(vm => vm.GoLastAsync()), Open, new CommandGesture(Key.End)))
            .Add(Add("go-to-page", now(vm => vm.RequestGoToPageFocus()), Open, Primary(Key.G)))
            .Add(Add("find", now(vm => vm.OpenFind()), when(vm => vm.IsSearchAvailable)))
            .Add(Add("find-next", run(vm => vm.FindNextAsync()), when(vm => vm.IsFindOpen),
                new CommandGesture(Key.F3)))
            .Add(Add("find-previous", run(vm => vm.FindPreviousAsync()), when(vm => vm.IsFindOpen),
                new CommandGesture(Key.F3, Shift: true)))
            .Add(Add("history-back", run(vm => vm.HistoryBackAsync()), when(vm => vm.CanHistoryBack),
                Primary(Key.OemOpenBrackets)))
            .Add(Add("history-forward", run(vm => vm.HistoryForwardAsync()), when(vm => vm.CanHistoryForward),
                Primary(Key.OemCloseBrackets)))
            .Add(Add("toggle-bookmark", run(vm => vm.ToggleBookmarkAsync()), Open, Primary(Key.B)))
            .Add(Add("show-bookmarks", now(vm => vm.OpenBookmarkPanel()), Open, Primary(Key.B, shift: true)))
            .Add(Add("capture-citation", run(vm => vm.CaptureCitationAsync()), Open, Primary(Key.C, shift: true)))
            .Add(Add("highlight", run(vm => vm.CreateHighlightAsync()), Open, Primary(Key.H, shift: true)))
            .Add(Add("zoom-in", now(vm => vm.ZoomIn()), Open, Primary(Key.OemPlus)))
            .Add(Add("zoom-out", now(vm => vm.ZoomOut()), Open, Primary(Key.OemMinus)))
            .Add(Add("zoom-fit-width", now(vm => vm.SetZoomMode(ZoomMode.FitWidth)), Open, Primary(Key.D0)))
            .Add(Add("layout-cycle", now(CycleLayout), Open, Primary(Key.L, shift: true)))
            .Add(new ShellCommand(
                SidePanelId,
                "Command.Reader.side-panel",
                CommandGroup.Reader,
                now(vm => vm.ToggleSidePanel()),
                Open,
                new CommandGesture(Key.F4)));
        return registry;
    }

    private static void CycleLayout(ReaderViewModel vm) =>
        vm.SetDisplayMode(vm.DisplayMode switch
        {
            DisplayMode.SinglePage => DisplayMode.TwoPage,
            DisplayMode.TwoPage => DisplayMode.Continuous,
            _ => DisplayMode.SinglePage,
        });
}
