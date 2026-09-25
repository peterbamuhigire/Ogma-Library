using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Sept-23 Phase 07 (K11, K12, K17, UX-003): the routed shell in the real window. Every rail
/// destination and every primary Library action is on screen and invocable (or offered by the
/// "More" menu), drawers are exclusive, standalone mode shows no dead entries and the command
/// palette opens, runs a command and closes.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class NavigationTests
{
    private static readonly string[] Destinations =
    [
        "Shell.Nav.Library", "Shell.Nav.Search", "Shell.Nav.Reading", "Shell.Nav.Advisor",
        "Shell.Nav.Collections", "Shell.Nav.Activity", "Shell.Nav.Settings",
    ];

    private static readonly string[] DeadInStandalone =
    [
        "Shell.Nav.Classroom", "Shell.Nav.StudentSearch", "Shell.More.Sharing", "Catalogue.View.Shelf3D",
    ];

    private static readonly string[] PrimaryActions =
    [
        "Shell.Action.ChooseFolder", "Shell.Action.Filter", "Catalogue.View.Grid", "Catalogue.View.List",
        "Catalogue.View.Directory", "Shell.Action.Rescan", "Catalogue.Filter.Sort", "Shell.Action.Folders",
    ];

    /// <summary>Oracle: each destination is reachable and shows its heading; each primary action is reachable or in More.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Navigation")]
    [Trait("Tag", "Navigation")]
    public void Navigation_EveryDestinationAndPrimaryActionIsReachable(string size) =>
        Journey.Run("Navigation", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;
            context.Shot("library");

            var reached = new List<string>();
            foreach (string id in Destinations)
            {
                AutomationElement item = Uia.WaitFor(window, id);
                Visibility.AssertReachable(context.Window, item, id);
                string label = item.Properties.Name.ValueOrDefault ?? string.Empty;
                Uia.Activate(item);
                bool shown = Uia.Poll(() => Uia.TryFind(window, "Shell.Heading")?.Properties.Name.ValueOrDefault == label, TimeSpan.FromSeconds(10));
                Assert.True(shown, $"{id} did not show its destination heading '{label}' [K11].");
                Visibility.AssertVisiblyPainted(context.Window, Uia.WaitFor(window, "Shell.Heading"), "heading of " + label);
                reached.Add(label);
            }

            context.Record("navigation.reached", reached);
            context.Shot("settings");
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Library"));

            var inMenu = new List<string>();
            AutomationElement more = Uia.WaitFor(window, "Shell.Action.More");
            Visibility.AssertReachable(context.Window, more, "More actions");
            foreach (string id in PrimaryActions)
            {
                AutomationElement action = Uia.WaitFor(window, id);
                if (IsOnScreen(context.Window, action))
                {
                    Visibility.AssertReachable(context.Window, action, id);
                    continue;
                }

                string name = action.Properties.Name.ValueOrDefault ?? id;
                Assert.True(MoreMenuOffers(context, more, name), $"{id} ('{name}') is neither on screen nor in the More menu [K11].");
                inMenu.Add(id);
            }

            context.Record("navigation.inMoreMenu", inMenu);
        });

    /// <summary>Oracle: opening one drawer closes the other, and standalone mode shows no classroom or 3D entry.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Category", "DeadEntries")]
    [Trait("Journey", "Navigation")]
    [Trait("Tag", "Navigation")]
    public void Navigation_DrawersAreExclusiveAndStandaloneHasNoDeadEntries(string size) =>
        Journey.Run("Navigation", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;

            foreach (string dead in DeadInStandalone)
            {
                AutomationElement? element = Uia.TryFind(window, dead);
                Assert.True(element is null || element.Properties.IsOffscreen.ValueOrDefault, $"Dead entry {dead} is shown in standalone mode [K17].");
            }

            Uia.Activate(Uia.WaitFor(window, "Shell.Action.Filter"));
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Catalogue.Filter.Title") is not null, TimeSpan.FromSeconds(5)), "The filter drawer did not open.");
            context.Shot("filter-drawer");

            AutomationElement folders = Uia.WaitFor(window, "Shell.Action.Folders");
            if (IsOnScreen(context.Window, folders))
            {
                Uia.Activate(folders);
            }
            else
            {
                Assert.True(MoreMenuOffers(context, Uia.WaitFor(window, "Shell.Action.More"), folders.Properties.Name.ValueOrDefault ?? "Folders", invoke: true));
            }

            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Catalogue.Filter.Title") is null, TimeSpan.FromSeconds(5)), "Two drawers are open at once [K12].");
            Assert.Equal(
                folders.Properties.Name.ValueOrDefault,
                Uia.WaitFor(window, "Shell.Drawer.Title").Properties.Name.ValueOrDefault);
            context.Shot("folders-drawer");

            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Search"));
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Shell.Drawer.Title") is null, TimeSpan.FromSeconds(5)), "A Library drawer stayed open on another destination [K12].");

            // G7 route: the Advisor explains the missing AI provider and routes to Settings.
            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Advisor"));
            AutomationElement route = Uia.WaitFor(window, "Advisor.SettingsRoute");
            Visibility.AssertReachable(context.Window, route, "Advisor route to Settings");
            Uia.Activate(route);
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Settings.Capabilities") is not null, TimeSpan.FromSeconds(5)), "The Settings route shows no capability list.");
        });

    /// <summary>Oracle: Ctrl+K opens the palette, typing and Enter run a command and close it; Escape closes it.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Category", "KeyboardOnly")]
    [Trait("Journey", "Navigation")]
    [Trait("Tag", "Navigation")]
    public void Navigation_CommandPaletteRunsCommandsAndCloses(string size) =>
        Journey.Run("Navigation", size, JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;
            Native.ForceForeground(context.RequireApp.Handle);
            Uia.WaitFor(window, "Shell.Nav.Library").Focus();

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
            AutomationElement query = Uia.WaitFor(window, "Shell.Palette.Query");
            context.Shot("palette");
            Keyboard.Type("settings");
            Thread.Sleep(300);
            Keyboard.Press(VirtualKeyShort.RETURN);
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Shell.Palette.Query") is null, TimeSpan.FromSeconds(5)), "The palette stayed open after running a command.");
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Settings.Capabilities") is not null, TimeSpan.FromSeconds(5)), "The palette did not run 'Go to Settings'.");

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
            Uia.WaitFor(window, "Shell.Palette.Query");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Shell.Palette.Query") is null, TimeSpan.FromSeconds(5)), "Escape did not close the palette.");

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_1);
            Assert.True(Uia.Poll(() => Uia.TryFind(window, "Catalogue.EmptyState.Heading") is not null, TimeSpan.FromSeconds(5)), "Ctrl+1 did not return to the Library.");
            GC.KeepAlive(query);
        });

    private static bool IsOnScreen(UiWindow window, AutomationElement element)
    {
        Rectangle bounds = element.BoundingRectangle;
        return !element.Properties.IsOffscreen.ValueOrDefault &&
               bounds.Width > 0 && bounds.Height > 0 &&
               window.ClientRectangle.Contains(bounds);
    }

    private static bool MoreMenuOffers(JourneyContext context, AutomationElement more, string name, bool invoke = false)
    {
        AutomationElement window = context.RequireApp.MainWindow;

        // A real click: the flyout opens from pointer or keyboard activation of the button.
        Native.ForceForeground(context.RequireApp.Handle);
        Rectangle r = more.BoundingRectangle;
        Mouse.Click(new Point(r.X + (r.Width / 2), r.Y + (r.Height / 2)));
        AutomationElement? item = null;
        bool found = Uia.Poll(
            () =>
            {
                int pid = context.RequireApp.Process.Id;
                // Avalonia hosts flyouts in popup windows: search them and the main window.
                item = context.RequireApp.Automation.GetDesktop()
                    .FindAllChildren(cf => cf.ByProcessId(pid))
                    .Append(window)
                    .SelectMany(top => top.FindAllDescendants(cf => cf.ByName(name)))
                    .FirstOrDefault(candidate => candidate.ControlType == ControlType.MenuItem);
                return item is not null;
            },
            TimeSpan.FromSeconds(5));
        if (!found)
        {
            context.Shot("more-menu-missing");
            context.Dump("more-menu-missing");
            int pidDiag = context.RequireApp.Process.Id;
            context.Record(
                "more.topLevels",
                context.RequireApp.Automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pidDiag))
                    .Select(top => $"{top.ControlType}:{top.ClassName}:{top.Properties.Name.ValueOrDefault}:{top.FindAllDescendants().Length}")
                    .ToArray());
        }

        if (found && invoke)
        {
            // Avalonia menu items expose no UIA activation pattern (Phase 21); click them.
            Rectangle bounds = item!.BoundingRectangle;
            Mouse.Click(new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)));
        }
        else
        {
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Uia.Poll(() => Uia.TryFind(window, "Shell.More.OpenPdf") is null, TimeSpan.FromSeconds(3));
        }

        return found;
    }
}
