using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.Data.Sqlite;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>
/// Sept-23 Phase 14 (task 14.10): local semantic search in the real window. J-SEM-1 builds the
/// smart index with the built-in model and finds a book by similar meaning; J-SEM-2 turns smart
/// search off; J-SEM-N1 idles with no model and proves no embedding attempt or failure; J-SEM-N2
/// installs damaged model files and proves the error state and parked jobs. The model is copied
/// from <c>OGMA_TEST_EMBEDDING_MODEL_DIR</c> into the session's temporary data folder (never into
/// git or a library folder); without it J-SEM-1 is NOT ASSESSED.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed partial class SemanticSearchTests
{
    private const string ExpectedTitle = "The Physics of Everyday Light";
    private const string ConceptualQuery = "books about light and colour";
    private const string ModeSemantic = "Titles, authors, full text and meaning";
    private const string ModeKeyword = "Titles, authors and full text · Semantic search off";
    private const string ModelReady = "Ready. Smart search runs on this device";
    private const string ModelCorrupt = "The installed model files are damaged. Retry the download to replace them.";
    private const string IndexDone = "Every book is ready for smart search.";

    /// <summary>J-SEM-1: pre-seeded model, Build index, progress completes, top-three hit by similar meaning.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-SEM-1")]
    [Trait("Tag", "SemanticSearch")]
    public void JSem1_BuildIndex_ConceptualQueryFindsBookBySimilarMeaning(string size) =>
        Journey.Run("J-SEM-1", size, JourneySupport.Standard, context =>
        {
            SemanticModel.SeedInto(context.Session.DataDirectory);
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            AutomationElement window = context.RequireApp.MainWindow;

            OpenSearchSettings(context);
            Choose(window, "Settings.Search.Provider.BuiltIn");
            Assert.True(
                Uia.Poll(() => NameOf(window, "Settings.Search.Model.Status").StartsWith(ModelReady, StringComparison.Ordinal), TimeSpan.FromSeconds(90), intervalMs: 500),
                $"The pre-seeded model did not report Ready: '{NameOf(window, "Settings.Search.Model.Status")}'.");
            context.Mark("model.ready");
            context.Shot("settings-model-ready");

            AutomationElement build = Uia.WaitFor(window, "Settings.Search.Index.Build");
            bool pressed = build.IsEnabled;
            if (pressed)
            {
                Uia.Activate(build);
            }

            context.Record("index.buildPressed", pressed);
            IndexProgress progress = WaitForIndex(window, context);
            context.Record("index.final", progress.ToString());
            context.Shot("settings-index-complete");
            Assert.True(
                progress.Complete,
                $"The smart index did not complete: last progress '{progress.Text}' after {progress.Waited.TotalMinutes:F1} minutes.");

            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Search"));
            AutomationElement box = Uia.WaitFor(window, "Search.Box");
            var clock = Stopwatch.StartNew();
            Uia.SetText(box, ConceptualQuery);
            string[] top = [];
            bool found = Uia.Poll(
                () =>
                {
                    top = TopResults(window);
                    return top.Any(name => name.StartsWith(ExpectedTitle, StringComparison.OrdinalIgnoreCase));
                },
                TimeSpan.FromSeconds(60),
                intervalMs: 300);
            (string mode, string modeRoute) = ModeLabel(window);
            context.Record("search.conceptual", new { query = ConceptualQuery, expected = ExpectedTitle, found, ms = clock.ElapsedMilliseconds, top, mode, modeRoute });
            context.Record("jobs.embedding", EmbeddingJobs.Read(context.Session.DataDirectory));
            context.Shot("search-conceptual");
            Assert.True(found, $"'{ConceptualQuery}' → top 3 [{string.Join(" | ", top)}], expected '{ExpectedTitle}'.");

            string hit = top.First(name => name.StartsWith(ExpectedTitle, StringComparison.OrdinalIgnoreCase));
            Assert.Contains("similar meaning", hit, StringComparison.Ordinal);
            Assert.Equal(ModeSemantic, mode);
        });

    /// <summary>J-SEM-2: smart search off gives the keyword-only label and no failed work.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-SEM-2")]
    [Trait("Tag", "SemanticSearch")]
    public void JSem2_SmartSearchOff_KeywordOnlyLabelAndNoFailures(string size) =>
        Journey.Run("J-SEM-2", size, JourneySupport.Standard, context =>
        {
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            AutomationElement window = context.RequireApp.MainWindow;

            OpenSearchSettings(context);
            Choose(window, "Settings.Search.Provider.Off");
            AutomationElement keywordOnly = Uia.WaitFor(window, "Settings.Search.KeywordOnly", TimeSpan.FromSeconds(10));
            Visibility.AssertVisiblyPainted(context.Window, keywordOnly, "Keyword-only notice");
            context.Shot("settings-off");

            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Search"));
            Uia.SetText(Uia.WaitFor(window, "Search.Box"), "Lantern");
            bool keywordMode = Uia.Poll(() => ModeLabel(window).Text == ModeKeyword, TimeSpan.FromSeconds(20), intervalMs: 300);
            (string mode, string modeRoute) = ModeLabel(window);
            context.Record("search.mode", new { mode, modeRoute });
            context.Shot("search-keyword-only");
            Assert.True(keywordMode, $"Search mode label was '{mode}', expected '{ModeKeyword}'.");

            Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Activity"));
            int? failed = null;
            Uia.Poll(() => (failed = FailedCount(window)) is not null, TimeSpan.FromSeconds(20), intervalMs: 500);
            context.Record("activity.failed", failed);
            context.Shot("activity");
            Assert.True(failed == 0, $"The Activity Centre failure total was {(failed?.ToString(CultureInfo.InvariantCulture) ?? "not shown")}, expected 0.");
        });

    /// <summary>
    /// J-SEM-N1: no model and a long idle. The idle is real wall-clock time, not compressed:
    /// <c>OGMA_E2E_IDLE_MINUTES</c> (default 10) minutes after the catalogue settles.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-SEM-N1")]
    [Trait("Tag", "SemanticSearch")]
    public void JSemN1_NoModel_IdleMakesNoEmbeddingAttemptOrFailure() =>
        Journey.Run("J-SEM-N1", E2ESettings.Sizes[0].ToString(), JourneySupport.Standard, context =>
        {
            double minutes = double.Parse(Environment.GetEnvironmentVariable("OGMA_E2E_IDLE_MINUTES") is { Length: > 0 } text ? text : "10", CultureInfo.InvariantCulture);
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            context.Mark("idle.start");
            context.Record("idle.minutes", minutes);
            Thread.Sleep(TimeSpan.FromMinutes(minutes));
            context.Mark("idle.end");
            Assert.False(context.RequireApp.Process.HasExited, "The app exited during the idle wait.");

            EmbeddingJobs jobs = EmbeddingJobs.Read(context.Session.DataDirectory);
            context.Record("jobs.embedding", jobs);
            context.Shot("after-idle");
            Assert.Equal(0, jobs.Failed);
            Assert.Equal(0, jobs.Attempts);
            Assert.Equal(0, jobs.Completed);
        });

    /// <summary>J-SEM-N2: damaged model files show the error state and embedding jobs stay parked.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "J-SEM-N2")]
    [Trait("Tag", "SemanticSearch")]
    public void JSemN2_CorruptModel_ErrorStateAndJobsParked(string size) =>
        Journey.Run("J-SEM-N2", size, JourneySupport.Standard, context =>
        {
            SemanticModel.SeedCorruptInto(context.Session.DataDirectory);
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            AutomationElement window = context.RequireApp.MainWindow;

            OpenSearchSettings(context);
            Choose(window, "Settings.Search.Provider.BuiltIn");
            bool corrupt = Uia.Poll(() => NameOf(window, "Settings.Search.Model.Status") == ModelCorrupt, TimeSpan.FromSeconds(90), intervalMs: 500);
            string status = NameOf(window, "Settings.Search.Model.Status");
            context.Record("model.status", status);
            context.Shot("settings-model-corrupt");
            Assert.True(corrupt, $"Model status was '{status}', expected the damaged-files message.");

            EmbeddingJobs jobs = default;
            bool queued = Uia.Poll(() => (jobs = EmbeddingJobs.Read(context.Session.DataDirectory)).Total > 0, TimeSpan.FromMinutes(3), intervalMs: 2000);
            Thread.Sleep(TimeSpan.FromSeconds(30));
            jobs = EmbeddingJobs.Read(context.Session.DataDirectory);
            context.Record("jobs.embedding", jobs);
            Assert.True(queued, "No embedding job was queued for the corpus, so parking cannot be shown.");
            Assert.Equal(0, jobs.Failed);
            Assert.Equal(0, jobs.Attempts);
            Assert.Equal(0, jobs.Completed);
            Assert.Equal(jobs.Total, jobs.Parked);
        });

    private static void OpenSearchSettings(JourneyContext context)
    {
        AutomationElement window = context.RequireApp.MainWindow;
        Uia.Activate(Uia.WaitFor(window, "Shell.Nav.Settings"));
        AutomationElement item = Uia.WaitFor(window, "Settings.Section.search");
        Visibility.AssertReachable(context.Window, item, "Settings section search");
        Uia.Activate(item);
        Assert.True(
            Uia.Poll(() => Uia.TryFind(window, "Settings.SectionTitle")?.Properties.Name.ValueOrDefault == item.Properties.Name.ValueOrDefault, TimeSpan.FromSeconds(5)),
            "Settings did not show the Search and AI section.");
    }

    private static void Choose(AutomationElement window, string automationId)
    {
        AutomationElement option = Uia.WaitFor(window, automationId);
        bool chosen = option.Patterns.SelectionItem.TryGetPattern(out var selection)
            ? selection.IsSelected.ValueOrDefault
            : option.Patterns.Toggle.TryGetPattern(out var toggle) && toggle.ToggleState.ValueOrDefault == ToggleState.On;
        if (!chosen)
        {
            Uia.Activate(option);
        }
    }

    private static string NameOf(AutomationElement window, string automationId)
    {
        AutomationElement? element = Uia.TryFind(window, automationId);
        if (element is null)
        {
            return string.Empty;
        }

        string? name = element.Properties.Name.ValueOrDefault;
        return string.IsNullOrEmpty(name) ? Uia.GetText(element) ?? string.Empty : name;
    }

    /// <summary>
    /// The search mode label. The <c>Search.Mode</c> border is not exposed to UI Automation
    /// (K149), so the label's visible text element is used when the id is absent; the route is
    /// recorded with the evidence.
    /// </summary>
    private static (string Text, string Route) ModeLabel(AutomationElement window)
    {
        string byId = NameOf(window, "Search.Mode");
        if (byId.Length > 0)
        {
            return (byId, "automation-id");
        }

        string[] known = [ModeSemantic, ModeKeyword, "Searching titles, authors and full text", "Titles, authors and full text · Semantic index preparing"];
        foreach (AutomationElement text in window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
        {
            string name = text.Properties.Name.ValueOrDefault ?? string.Empty;
            if (known.Contains(name, StringComparer.Ordinal))
            {
                return (name, "visible-text (K149)");
            }
        }

        return (string.Empty, "absent");
    }

    private static string[] TopResults(AutomationElement window)
    {
        AutomationElement? results = Uia.TryFind(window, "Search.Results");
        return results is null
            ? []
            : Uia.ListItems(results).Take(3).Select(item => item.Properties.Name.ValueOrDefault ?? string.Empty).ToArray();
    }

    private static int? FailedCount(AutomationElement window)
    {
        foreach (AutomationElement text in window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
        {
            Match match = FailedPattern().Match(text.Properties.Name.ValueOrDefault ?? string.Empty);
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static IndexProgress WaitForIndex(AutomationElement window, JourneyContext context)
    {
        double minutes = double.Parse(Environment.GetEnvironmentVariable("OGMA_E2E_SEMANTIC_INDEX_MINUTES") is { Length: > 0 } text ? text : "20", CultureInfo.InvariantCulture);
        var clock = Stopwatch.StartNew();
        string last = string.Empty;
        var samples = new List<object>();
        while (clock.Elapsed < TimeSpan.FromMinutes(minutes))
        {
            last = NameOf(window, "Settings.Search.Index.Count");
            string eta = NameOf(window, "Settings.Search.Index.Eta");
            Match match = ProgressPattern().Match(last);
            if (samples.Count == 0 || clock.Elapsed.TotalSeconds >= samples.Count * 30)
            {
                samples.Add(new { s = (long)clock.Elapsed.TotalSeconds, last, eta });
            }

            bool counted = match.Success &&
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) > 0 &&
                match.Groups[1].Value == match.Groups[2].Value;
            if (counted || eta == IndexDone)
            {
                context.Record("index.samples", samples);
                context.Mark("index.complete");
                return new IndexProgress(true, last, clock.Elapsed);
            }

            Thread.Sleep(1000);
        }

        context.Record("index.samples", samples);
        return new IndexProgress(false, last, clock.Elapsed);
    }

    [GeneratedRegex(@"Failed \((\d+)\)")]
    private static partial Regex FailedPattern();

    [GeneratedRegex(@"(\d+) of (\d+) books ready")]
    private static partial Regex ProgressPattern();

    private sealed record IndexProgress(bool Complete, string Text, TimeSpan Waited);

    /// <summary>Embedding job totals read from the session's <c>catalogue.db</c>.</summary>
    private readonly record struct EmbeddingJobs(int Total, int Completed, int Failed, int Parked, int Attempts, int Started)
    {
        public static EmbeddingJobs Read(string dataDirectory)
        {
            string path = Path.Combine(dataDirectory, "catalogue.db");
            if (!File.Exists(path))
            {
                return default;
            }

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*), " +
                "COALESCE(SUM(Status = 2), 0), " +
                "COALESCE(SUM(Status IN (3, 5)), 0), " +
                "COALESCE(SUM(Status IN (0, 6, 7)), 0), " +
                "COALESCE(SUM(RetryCount), 0), " +
                "COALESCE(SUM(StartedUtc IS NOT NULL), 0) " +
                "FROM Jobs WHERE JobType IN ('EmbeddingJob', 'EmbeddingGeneration')";
            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();
            return new EmbeddingJobs(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5));
        }
    }

    /// <summary>Places the pinned embedding model in a session data folder.</summary>
    private static class SemanticModel
    {
        public static void SeedInto(string dataDirectory)
        {
            string source = Environment.GetEnvironmentVariable("OGMA_TEST_EMBEDDING_MODEL_DIR") ?? string.Empty;
            if (source.Length == 0 || !Directory.Exists(source))
            {
                throw new NotAssessedException("OGMA_TEST_EMBEDDING_MODEL_DIR is not set to a folder holding the pinned embedding model.");
            }

            foreach ((string relative, string target) in Targets(dataDirectory))
            {
                string from = Path.Combine(source, relative);
                if (!File.Exists(from))
                {
                    throw new NotAssessedException($"The model folder has no '{relative}'.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(from, target, overwrite: true);
            }
        }

        public static void SeedCorruptInto(string dataDirectory)
        {
            foreach ((_, string target) in Targets(dataDirectory))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, new byte[4096]);
            }
        }

        private static List<(string Relative, string Target)> Targets(string dataDirectory)
        {
            string manifestPath = Path.Combine(E2ESettings.RepositoryRoot, "src", "OgmaLibrary.Infrastructure", "AI", "Onnx", "Resources", "embedding-model-manifest.json");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            string modelId = manifest.RootElement.GetProperty("modelId").GetString()!;
            string revision = manifest.RootElement.GetProperty("revision").GetString()!;
            string root = Path.Combine(dataDirectory, "models", "embeddings", modelId.Replace("/", "--", StringComparison.Ordinal), revision);
            var targets = new List<(string Relative, string Target)>();
            foreach (JsonElement file in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                string relative = file.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar);
                targets.Add((relative, Path.Combine(root, relative)));
            }

            return targets;
        }
    }
}
