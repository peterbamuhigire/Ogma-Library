using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>
/// Thrown when a journey cannot be assessed with the requested parameters. The test then fails in
/// the raw runner (never a pass) and the results file records <c>NOT ASSESSED</c>.
/// </summary>
public sealed class NotAssessedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public NotAssessedException(string message)
        : base("NOT ASSESSED: " + message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public NotAssessedException()
    {
    }

    /// <summary>Creates the exception.</summary>
    public NotAssessedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The run parameters a journey can honour; anything else is reported NOT ASSESSED.</summary>
public sealed record JourneySupport(
    bool Themes = true,
    bool KeyboardOnly = false,
    bool ClippingCheck = false,
    bool MockAi = false)
{
    /// <summary>The standard support set: both themes, English, 100 % text scale.</summary>
    public static JourneySupport Standard { get; } = new();

    /// <summary>The requested parameters this journey cannot honour.</summary>
    public IReadOnlyList<string> Unsupported()
    {
        var missing = new List<string>();
        if (!string.Equals(E2ESettings.Culture, "en", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add($"-Culture {E2ESettings.Culture} (the app has no culture override yet; Phase 22)");
        }

        if (E2ESettings.TextScale != 100)
        {
            missing.Add($"-TextScale {E2ESettings.TextScale} (OS text scaling is not automated yet; Phase 21)");
        }

        if (!Themes && !string.Equals(E2ESettings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add($"-Themes {E2ESettings.Theme}");
        }

        if (E2ESettings.KeyboardOnly && !KeyboardOnly)
        {
            missing.Add("-KeyboardOnly (keyboard-only driving lands with Phase 21)");
        }

        if (E2ESettings.ClippingCheck && !ClippingCheck)
        {
            missing.Add("-ClippingCheck (clipping detection lands with Phase 22)");
        }

        if (E2ESettings.UseMockAiServer && !MockAi)
        {
            missing.Add("-UseMockAiServer (the mock provider lands with Phases 15-16)");
        }

        return missing;
    }
}

/// <summary>One journey execution at one window size: session, app, evidence and timings (T01.9).</summary>
public sealed class JourneyContext : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, object?> _timings = new(StringComparer.Ordinal);
    private bool _beforeCaptured;

    internal JourneyContext(string journey, string test, WindowSize size)
    {
        Journey = journey;
        Test = test;
        Size = size;
        Session = new E2ESession($"{journey}-{size}");
        EvidenceDirectory = Path.Combine(E2ESettings.ArtifactsDirectory, journey);
        Directory.CreateDirectory(EvidenceDirectory);
        _timings["journey"] = journey;
        _timings["test"] = test;
        _timings["size"] = size.ToString();
        _timings["exe"] = E2ESettings.ExePath;
        _timings["theme"] = E2ESettings.Theme;
    }

    /// <summary>The journey id (G1..G12 or a named scenario).</summary>
    public string Journey { get; }

    /// <summary>The xUnit test method.</summary>
    public string Test { get; }

    /// <summary>The window size.</summary>
    public WindowSize Size { get; }

    /// <summary>The isolated folders.</summary>
    public E2ESession Session { get; }

    /// <summary>The running app, if launched.</summary>
    public OgmaApp? App { get; private set; }

    /// <summary>The running app; fails when none is launched.</summary>
    public OgmaApp RequireApp => App ?? throw new InvalidOperationException("The app is not running.");

    /// <summary>The main window as a harness window.</summary>
    public UiWindow Window => RequireApp.Window;

    /// <summary>Where screenshots, dumps and timings go.</summary>
    public string EvidenceDirectory { get; }

    /// <summary>Elapsed journey time.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    /// <summary>Launches (or relaunches) the app for this session and sizes the window.</summary>
    public OgmaApp Launch(IReadOnlyDictionary<string, string>? environment = null, string label = "launch")
    {
        if (App is not null)
        {
            Record(label + ".previousClose", App.Close());
            App.Dispose();
        }

        SeedPreferences();
        App = OgmaApp.Launch(Session, environment);
        Record(label + ".timeToWindowMs", (long)App.TimeToWindow.TotalMilliseconds);
        App.Resize(Size);
        if (!_beforeCaptured)
        {
            _beforeCaptured = true;
            Shot("before");
        }

        return App;
    }

    /// <summary>Records a timing or fact into <c>timings.json</c>.</summary>
    public void Record(string key, object? value) => _timings[key] = value;

    /// <summary>Records the elapsed journey milliseconds under <paramref name="key"/>.</summary>
    public long Mark(string key)
    {
        long ms = (long)_clock.Elapsed.TotalMilliseconds;
        _timings[key + "Ms"] = ms;
        return ms;
    }

    /// <summary>Whether <see cref="Mark"/> has recorded <paramref name="key"/>.</summary>
    public bool HasMark(string key) => _timings.ContainsKey(key + "Ms");

    /// <summary>Saves <c>{label}-{size}.png</c> and returns its path (null when no window).</summary>
    public string? Shot(string label)
    {
        if (App is null || !App.IsAlive)
        {
            return null;
        }

        try
        {
            return ScreenCapture.SaveWindow(App.Handle, Path.Combine(EvidenceDirectory, $"{label}-{Size}.png"));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or ArgumentException or InvalidOperationException)
        {
            Record("shot." + label + ".error", ex.Message);
            return null;
        }
    }

    /// <summary>Writes the UIA tree of the app's windows to <c>uia-{label}-{size}.json</c>.</summary>
    public string? Dump(string label)
    {
        if (App is null || !App.IsAlive)
        {
            return null;
        }

        string path = Path.Combine(EvidenceDirectory, $"uia-{label}-{Size}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(UiaAudit.Snapshot(App.MainWindow), JsonOptions));
        return path;
    }

    /// <summary>Runs the T01.7 name audit on the current screen when <c>-UiaAudit</c> is requested.</summary>
    public void AuditIfRequested()
    {
        if (!E2ESettings.UiaAudit || App is null)
        {
            return;
        }

        IReadOnlyList<string> violations = UiaAudit.Violations(App.MainWindow);
        Record("uiaAudit.violations", violations);
        Assert.True(violations.Count == 0, "UIA audit: " + string.Join("; ", violations.Take(25)));
    }

    /// <summary>Copies the app's log folder into the evidence directory.</summary>
    public void CopyLogs(string label)
    {
        string logs = Path.Combine(Session.DataDirectory, "logs");
        if (!Directory.Exists(logs))
        {
            return;
        }

        string target = Path.Combine(EvidenceDirectory, $"logs-{label}-{Size}");
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(logs))
        {
            try
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            }
            catch (IOException)
            {
                // The sink may hold the file; the next copy attempt after close succeeds.
            }
        }
    }

    /// <summary>The app's log lines so far (JSON lines).</summary>
    public IReadOnlyList<string> LogLines()
    {
        string logs = Path.Combine(Session.DataDirectory, "logs");
        if (!Directory.Exists(logs))
        {
            return [];
        }

        var lines = new List<string>();
        foreach (string file in Directory.EnumerateFiles(logs, "*.log"))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    internal void Complete(string status, string? message)
    {
        _timings["status"] = status;
        _timings["totalMs"] = (long)_clock.Elapsed.TotalMilliseconds;
        if (App is not null)
        {
            CloseResult close = App.Close();
            _timings["close"] = close;
            if (status != "PASS")
            {
                CopyLogs("failure");
            }

            App.Dispose();
            App = null;
        }

        File.WriteAllText(
            Path.Combine(EvidenceDirectory, $"timings-{Test}-{Size}.json"),
            JsonSerializer.Serialize(_timings, JsonOptions));
        JourneyResults.Append(Journey, Test, Size.ToString(), status, message, (long)_clock.Elapsed.TotalMilliseconds, EvidenceDirectory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        App?.Dispose();
        Session.Dispose();
    }

    private void SeedPreferences()
    {
        if (!string.Equals(E2ESettings.Theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string path = Path.Combine(Session.DataDirectory, "user-preferences.json");
        if (!File.Exists(path))
        {
            // UserTheme.Dark = 1 (OgmaLibrary.Application.UserPreferences), web JSON defaults.
            File.WriteAllText(path, "{\"theme\":1,\"density\":0}");
        }
    }
}

/// <summary>Appends one line per journey execution to <c>results.jsonl</c> (read by the wrapper).</summary>
public static class JourneyResults
{
    private static readonly object Gate = new();

    /// <summary>Appends a result row.</summary>
    public static void Append(string journey, string test, string size, string status, string? message, long elapsedMs, string evidence)
    {
        string line = JsonSerializer.Serialize(new
        {
            journey,
            test,
            size,
            status,
            message = message is null ? null : (message.Length > 600 ? message[..600] : message),
            elapsedMs,
            evidence = Path.GetRelativePath(E2ESettings.RepositoryRoot, evidence),
            utc = DateTime.UtcNow,
        });
        lock (Gate)
        {
            Directory.CreateDirectory(E2ESettings.ArtifactsDirectory);
            File.AppendAllText(Path.Combine(E2ESettings.ArtifactsDirectory, "results.jsonl"), line + Environment.NewLine);
        }
    }
}

/// <summary>Runs a journey with evidence capture and honest reporting.</summary>
public static class Journey
{
    /// <summary>
    /// Runs <paramref name="body"/> at <paramref name="size"/>. Unsupported run parameters are
    /// NOT ASSESSED; failures capture a screenshot, a UIA dump and the app log.
    /// </summary>
    public static void Run(string journey, string size, JourneySupport support, Action<JourneyContext> body, [System.Runtime.CompilerServices.CallerMemberName] string test = "")
    {
        IReadOnlyList<string> unsupported = support.Unsupported();
        var windowSize = WindowSize.Parse(size);
        if (unsupported.Count > 0)
        {
            string reason = string.Join("; ", unsupported);
            JourneyResults.Append(journey, test, size, "NOT ASSESSED", reason, 0, E2ESettings.ArtifactsDirectory);
            throw new NotAssessedException(reason);
        }

        using var context = new JourneyContext(journey, test, windowSize);
        try
        {
            body(context);
            context.AuditIfRequested();
            context.Shot("after");
            context.Complete("PASS", null);
        }
        catch (NotAssessedException ex)
        {
            context.Complete("NOT ASSESSED", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            context.Shot("failure");
            try
            {
                context.Dump("failure");
            }
            catch (Exception dumpError) when (dumpError is System.Runtime.InteropServices.COMException or IOException
                                                  or FlaUI.Core.Exceptions.ElementNotAvailableException)
            {
                context.Record("dump.error", dumpError.Message);
            }

            context.Complete("FAIL", ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    /// <summary>Registers a journey owned by a later phase: always NOT ASSESSED, never a pass.</summary>
    public static void Placeholder(string journey, string owner, [System.Runtime.CompilerServices.CallerMemberName] string test = "")
    {
        string reason = $"{journey} is implemented by {owner}; registered as a placeholder in Phase 01.";
        foreach (WindowSize size in E2ESettings.Sizes)
        {
            JourneyResults.Append(journey, test, size.ToString(), "NOT ASSESSED", reason, 0, E2ESettings.ArtifactsDirectory);
        }

        throw new NotAssessedException(reason);
    }
}

/// <summary>T01.7 accessible-name audit and UIA snapshots.</summary>
public static partial class UiaAudit
{
    private static readonly ControlType[] Interactive =
    [
        ControlType.Button, ControlType.Edit, ControlType.CheckBox, ControlType.ListItem, ControlType.MenuItem,
        ControlType.ComboBox, ControlType.Hyperlink, ControlType.RadioButton, ControlType.TabItem, ControlType.SplitButton,
    ];

    /// <summary>Whether a name is a C# record dump such as <c>BookSummaryProjection { BookId = … }</c>.</summary>
    public static bool IsRecordDump(string? name) => name is not null && RecordDumpPattern().IsMatch(name);

    /// <summary>Unnamed interactive controls and record-dump names under <paramref name="root"/> (on-screen only).</summary>
    public static IReadOnlyList<string> Violations(AutomationElement root)
    {
        var violations = new List<string>();
        foreach (AutomationElement element in root.FindAllDescendants())
        {
            try
            {
                if (element.Properties.IsOffscreen.ValueOrDefault)
                {
                    continue;
                }

                string? name = element.Properties.Name.ValueOrDefault;
                ControlType type = element.Properties.ControlType.ValueOrDefault;
                if (IsRecordDump(name))
                {
                    violations.Add($"record-dump name: {Uia.Describe(element)}");
                }
                else if (Interactive.Contains(type) && string.IsNullOrWhiteSpace(name))
                {
                    violations.Add($"unnamed {type}: id='{element.Properties.AutomationId.ValueOrDefault}'");
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // The element went away during the walk.
            }
        }

        return violations;
    }

    /// <summary>A flat JSON-friendly snapshot of the window's UIA tree.</summary>
    public static IReadOnlyList<object> Snapshot(AutomationElement root)
    {
        var rows = new List<object>();
        foreach (AutomationElement element in root.FindAllDescendants())
        {
            try
            {
                rows.Add(new
                {
                    type = element.Properties.ControlType.ValueOrDefault.ToString(),
                    name = element.Properties.Name.ValueOrDefault,
                    id = element.Properties.AutomationId.ValueOrDefault,
                    enabled = element.Properties.IsEnabled.ValueOrDefault,
                    offscreen = element.Properties.IsOffscreen.ValueOrDefault,
                    bounds = element.Properties.BoundingRectangle.ValueOrDefault.ToString(),
                });
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Skip elements that disappeared.
            }
        }

        return rows;
    }

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9_]*\s*\{", RegexOptions.None, 1000)]
    private static partial Regex RecordDumpPattern();
}
