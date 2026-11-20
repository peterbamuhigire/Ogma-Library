using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using OgmaLibrary.App.Navigation;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>The reader's actions in the shell command registry (Sept-23 Phase 12, READ-013, UX-005).</summary>
public sealed partial class ShellReaderNavigationTests
{
    [AvaloniaFact]
    public void Registry_NoDuplicateGestures()
    {
        using MainShellViewModel shell = NewRegistryShell();

        var duplicates = shell.Commands.All
            .SelectMany(command => command.Gestures.Select(gesture => (gesture, command.Id)))
            .GroupBy(entry => entry.gesture)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(entry => entry.Id))}")
            .ToList();

        Assert.True(duplicates.Count == 0, "Gestures used twice: " + string.Join("; ", duplicates));
        Assert.Contains(shell.Commands.All, command => command.Group == CommandGroup.Reader);
    }

    [AvaloniaFact]
    public void EveryReaderToolbarAction_HasShortcutOrAccessKey()
    {
        using MainShellViewModel shell = NewRegistryShell();
        string axaml = File.ReadAllText(FindFromRepositoryRoot(
            "src", "OgmaLibrary.App", "Views", "Reader", "ReaderView.axaml"));

        // The toolbar is everything before the find bar. Sub-menu items are reached through their parent.
        int toolbarEnd = axaml.IndexOf("AutomationId=\"Reader.Find.Box\"", StringComparison.Ordinal);
        Assert.True(toolbarEnd > 0, "The find bar marker moved; update this test.");
        string toolbar = axaml[..toolbarEnd];

        List<string> actions = Regex
            .Matches(
                toolbar,
                @"<(?:Button|ToggleButton|DropDownButton|SplitButton|TextBox|NumericUpDown)\b[^>]*?AutomationId=""(Reader\.[^""]+)""",
                RegexOptions.Singleline)
            .Select(match => match.Groups[1].Value)
            .ToList();
        Assert.NotEmpty(actions);

        foreach (string id in actions)
        {
            Assert.True(
                ReaderCommands.ToolbarShortcuts.TryGetValue(id, out string? how),
                $"Toolbar control {id} has no shortcut or access key entry in ReaderCommands.ToolbarShortcuts.");
            if (how!.StartsWith("cmd:", StringComparison.Ordinal))
            {
                string commandId = how["cmd:".Length..];
                ShellCommand command = Assert.Single(shell.Commands.All, c => c.Id == commandId);
                Assert.NotEmpty(command.Gestures);
            }
            else
            {
                Assert.StartsWith("access:", how, StringComparison.Ordinal);
                string key = how["access:".Length..];
                Assert.Contains($"HotKey=\"{key}\"", toolbar, StringComparison.Ordinal);
            }
        }

        foreach (string id in ReaderCommands.ToolbarShortcuts.Keys)
        {
            Assert.Contains($"AutomationId=\"{id}\"", axaml, StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    public void ReaderCommands_AllHaveEnglishAndFrenchLabels()
    {
        using MainShellViewModel shell = NewRegistryShell();
        var localization = new InMemoryLocalizationService();
        foreach (ShellCommand command in shell.Commands.All.Where(c => c.Group == CommandGroup.Reader))
        {
            foreach (string culture in new[] { "en", "fr" })
            {
                localization.SetCulture(culture);
                string label = localization[command.LabelKey];
                Assert.False(string.IsNullOrWhiteSpace(label) || label == command.LabelKey, $"{culture}: {command.Id}");
            }
        }
    }

    private static MainShellViewModel NewRegistryShell()
    {
        var localization = new InMemoryLocalizationService();
        var readModel = new EmptyCatalogueReadModel();
        var catalogue = new OgmaLibrary.App.ViewModels.Catalogue.CatalogueViewModel(
            readModel, new NullNavigation(), localization);
        var bookDetail = new OgmaLibrary.App.ViewModels.Catalogue.BookDetailViewModel(
            readModel, new NullNavigation(), localization);
        var shelfSidebar = new OgmaLibrary.App.ViewModels.Catalogue.ShelfSidebarViewModel(
            readModel, new NoOpCatalogueWriteService(), localization, new CatalogueFilterViewModel());
        return new MainShellViewModel(localization, catalogue, bookDetail, shelfSidebar);
    }

    private static string FindFromRepositoryRoot(params string[] parts)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "OgmaLibrary.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return Path.Combine([dir!, .. parts]);
    }
}
