using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;

namespace OgmaLibrary.App.Navigation;

/// <summary>Palette grouping of shell commands.</summary>
public enum CommandGroup
{
    /// <summary>Go to a destination.</summary>
    Navigate = 0,

    /// <summary>Library actions.</summary>
    Library = 1,

    /// <summary>Reader actions.</summary>
    Reader = 2,

    /// <summary>Appearance and layout.</summary>
    View = 3,

    /// <summary>Application actions.</summary>
    App = 4,
}

/// <summary>
/// A keyboard gesture. <see cref="Primary"/> is Ctrl on Windows and Linux and Cmd on macOS;
/// either modifier is accepted on every platform so shortcuts survive remote-desktop mappings.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Primary">Whether Ctrl/Cmd is required.</param>
/// <param name="Shift">Whether Shift is required.</param>
/// <param name="Alt">Whether Alt is required.</param>
public sealed record CommandGesture(Key Key, bool Primary = false, bool Shift = false, bool Alt = false)
{
    /// <summary>Whether a key event matches this gesture exactly.</summary>
    /// <param name="key">The pressed key.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <returns><see langword="true"/> on an exact match.</returns>
    public bool Matches(Key key, KeyModifiers modifiers)
    {
        if (key != Key)
        {
            return false;
        }

        bool primary = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        return primary == Primary &&
               modifiers.HasFlag(KeyModifiers.Shift) == Shift &&
               modifiers.HasFlag(KeyModifiers.Alt) == Alt;
    }

    /// <summary>Formats the gesture for display, for example "Ctrl+1" or "⌘1".</summary>
    /// <param name="mac">Whether to use macOS symbols.</param>
    /// <returns>The display text.</returns>
    public string Format(bool mac)
    {
        string key = Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(Key - Key.D0)).ToString(CultureInfo.InvariantCulture),
            Key.Left => mac ? "←" : "Left",
            Key.Right => mac ? "→" : "Right",
            Key.OemQuestion => "/",
            _ => Key.ToString(),
        };

        if (mac)
        {
            return string.Concat(Alt ? "⌥" : string.Empty, Shift ? "⇧" : string.Empty, Primary ? "⌘" : string.Empty, key);
        }

        var parts = new List<string>(4);
        if (Primary)
        {
            parts.Add("Ctrl");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        parts.Add(key);
        return string.Join('+', parts);
    }
}

/// <summary>
/// A shell command registered once and surfaced by the palette, the keyboard map and menus
/// (Sept-23 Phase 07, T07.7), so the three can never drift apart.
/// </summary>
public sealed class ShellCommand
{
    /// <summary>Initializes a new instance of the <see cref="ShellCommand"/> class.</summary>
    /// <param name="id">The stable command identifier.</param>
    /// <param name="labelKey">The localisation key of the label.</param>
    /// <param name="group">The palette group.</param>
    /// <param name="execute">The action; it receives the window when one is available.</param>
    /// <param name="isAvailable">Whether the command can run now; hidden from the palette otherwise.</param>
    /// <param name="gestures">The keyboard gestures, first one shown as the hint.</param>
    public ShellCommand(
        string id,
        string labelKey,
        CommandGroup group,
        Func<TopLevel?, Task> execute,
        Func<bool>? isAvailable = null,
        params CommandGesture[] gestures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        ArgumentNullException.ThrowIfNull(execute);
        Id = id;
        LabelKey = labelKey;
        Group = group;
        Execute = execute;
        IsAvailable = isAvailable ?? (() => true);
        Gestures = gestures;
    }

    /// <summary>The stable command identifier.</summary>
    public string Id { get; }

    /// <summary>The localisation key of the label.</summary>
    public string LabelKey { get; }

    /// <summary>The palette group.</summary>
    public CommandGroup Group { get; }

    /// <summary>The action.</summary>
    public Func<TopLevel?, Task> Execute { get; }

    /// <summary>Whether the command can run now.</summary>
    public Func<bool> IsAvailable { get; }

    /// <summary>The keyboard gestures.</summary>
    public IReadOnlyList<CommandGesture> Gestures { get; }

    /// <summary>Formats the first gesture, or an empty string.</summary>
    /// <param name="mac">Whether to use macOS symbols.</param>
    /// <returns>The hint.</returns>
    public string GestureText(bool mac) => Gestures.Count == 0 ? string.Empty : Gestures[0].Format(mac);
}

/// <summary>The single registry of shell commands (Sept-23 Phase 07, T07.7).</summary>
public sealed class CommandRegistry
{
    private const int MaxRecent = 5;
    private readonly List<ShellCommand> _commands = [];
    private readonly List<string> _recent = [];

    /// <summary>All registered commands in registration order.</summary>
    public IReadOnlyList<ShellCommand> All => _commands;

    /// <summary>The most recently executed command identifiers, newest first.</summary>
    public IReadOnlyList<string> Recent => _recent;

    /// <summary>Registers a command; identifiers must be unique.</summary>
    /// <param name="command">The command.</param>
    /// <returns>This registry.</returns>
    public CommandRegistry Add(ShellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_commands.Any(existing => existing.Id == command.Id))
        {
            throw new InvalidOperationException($"Duplicate command id '{command.Id}'.");
        }

        _commands.Add(command);
        return this;
    }

    /// <summary>Finds a command by identifier.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The command, or <see langword="null"/>.</returns>
    public ShellCommand? Find(string id) => _commands.FirstOrDefault(command => command.Id == id);

    /// <summary>Finds the available command bound to a key gesture.</summary>
    /// <param name="key">The pressed key.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <returns>The command, or <see langword="null"/>.</returns>
    public ShellCommand? MatchGesture(Key key, KeyModifiers modifiers) =>
        _commands.FirstOrDefault(command =>
            command.Gestures.Any(gesture => gesture.Matches(key, modifiers)) && command.IsAvailable());

    /// <summary>Records a command as recently used.</summary>
    /// <param name="id">The identifier.</param>
    public void MarkUsed(string id)
    {
        _recent.Remove(id);
        _recent.Insert(0, id);
        if (_recent.Count > MaxRecent)
        {
            _recent.RemoveAt(_recent.Count - 1);
        }
    }

    /// <summary>
    /// Returns the available commands that match <paramref name="query"/>, best first. An empty
    /// query lists recent commands first, then the rest in registration order.
    /// </summary>
    /// <param name="query">The typed query.</param>
    /// <param name="label">Resolves a command's localised label.</param>
    /// <returns>The matching commands.</returns>
    public IReadOnlyList<ShellCommand> Search(string? query, Func<ShellCommand, string> label)
    {
        ArgumentNullException.ThrowIfNull(label);
        List<ShellCommand> available = _commands.Where(command => command.IsAvailable()).ToList();
        string trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return available
                .OrderBy(command => _recent.IndexOf(command.Id) is var index and >= 0 ? index : MaxRecent + 1)
                .ThenBy(command => _commands.IndexOf(command))
                .ToList();
        }

        return available
            .Select(command => (Command: command, Score: FuzzyMatcher.Score(trimmed, label(command))))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => _commands.IndexOf(match.Command))
            .Select(match => match.Command)
            .ToList();
    }
}

/// <summary>Scores how well a query matches a label (substring, word prefixes, then subsequence).</summary>
public static class FuzzyMatcher
{
    /// <summary>Scores a match; 0 means no match, higher is better.</summary>
    /// <param name="query">The query.</param>
    /// <param name="text">The candidate text.</param>
    /// <returns>The score.</returns>
    public static int Score(string query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(text);
        string q = Normalize(query);
        string t = Normalize(text);
        if (q.Length == 0 || t.Length == 0)
        {
            return 0;
        }

        int index = t.IndexOf(q, StringComparison.Ordinal);
        if (index >= 0)
        {
            return 1000 - Math.Min(index * 4, 200) - Math.Min(t.Length - q.Length, 100);
        }

        string[] words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] parts = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.All(part => words.Any(word => word.StartsWith(part, StringComparison.Ordinal))))
        {
            return 600;
        }

        int gaps = 0;
        int position = 0;
        foreach (char c in q.Where(c => c != ' '))
        {
            int found = t.IndexOf(c, position);
            if (found < 0)
            {
                return 0;
            }

            gaps += found - position;
            position = found + 1;
        }

        return Math.Max(1, 300 - (gaps * 5));
    }

    private static string Normalize(string value)
    {
        string decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Trim();
    }
}
