using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G8: resilience — the app survives a killed PDF worker and a corrupt settings file.</summary>
[Collection(RealWindowTests.Name)]
public sealed class G08ResilienceTests
{
    /// <summary>
    /// Oracle: after the PDF worker is killed mid-read the app stays alive, the next page renders
    /// or a recoverable message is shown, and the failure is logged (Warning or above).
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G8")]
    [Trait("Tag", "Reader")]
    public void G8_WorkerKilledDuringReading_AppSurvivesAndLogs(string size) =>
        Journey.Run("G8", size, JourneySupport.Standard, context =>
        {
            CorpusReader probe = Corpus.Oracle.Reader;
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            Shell.OpenInReader(context, probe.Title);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement page = Uia.WaitFor(window, "Reader.Page");
            G03ReadTests.AssertPagePainted(context, page, "page before the worker is killed");

            IReadOnlyList<int> workers = context.RequireApp.WorkerProcessIds();
            context.Record("workers.killed", workers);
            Assert.True(workers.Count > 0, "No PDF worker process was running while a book was open.");
            int warningsBefore = WorkerWarnings(context);
            foreach (int id in workers)
            {
                using Process worker = Process.GetProcessById(id);
                worker.Kill();
            }

            context.Mark("worker.killed");
            PageTurn turn = Shell.TurnPage(context, TimeSpan.FromSeconds(20));
            context.Record("turnAfterKill", turn);
            bool recovered = Uia.Poll(
                () => (turn.Advanced &&
                       Uia.TryFind(window, "Reader.Page") is { } rendered &&
                       Visibility.Measure(context.Window, rendered, G03ReadTests.ReaderPageMinimumPainted, allowScrolledPartially: true).Passed) ||
                      Uia.TryFind(window, "Shell.Toast") is not null,
                TimeSpan.FromSeconds(20),
                intervalMs: 500);
            bool logged = Uia.Poll(() => WorkerWarnings(context) > warningsBefore, TimeSpan.FromSeconds(10), intervalMs: 500);
            context.Record("recovered", recovered);
            context.Record("logged", logged);
            context.Shot("after-worker-kill");
            Assert.True(context.RequireApp.IsAlive, "The app exited after the PDF worker was killed [K30].");
            Assert.True(recovered, "After the worker was killed the next page neither rendered nor produced a recoverable message.");
            Assert.True(logged, "The worker failure was not logged at Warning or above.");
        });

    /// <summary>
    /// Oracle: with a corrupt <c>library-settings.json</c> the app starts, shows a recoverable
    /// message (degraded panel or toast) and logs the event.
    /// </summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G8")]
    [Trait("Tag", "Settings")]
    public void G8_CorruptLibrarySettings_AppStartsWithRecoverableMessage(string size) =>
        Journey.Run("G8", size, JourneySupport.Standard, context =>
        {
            File.WriteAllText(Path.Combine(context.Session.DataDirectory, "library-settings.json"), "{ \"libraryRoot\": oops not json");
            context.Launch();
            Shell.WaitReady(context);
            AutomationElement window = context.RequireApp.MainWindow;
            AutomationElement? message = null;
            Uia.Poll(
                () => (message = Uia.TryFind(window, "Shell.Degraded") ?? Uia.TryFind(window, "Shell.Toast")) is not null,
                TimeSpan.FromSeconds(20));
            bool logged = context.LogLines().Any(line =>
                IsWarningOrAbove(line) && line.Contains("setting", StringComparison.OrdinalIgnoreCase));
            context.Record("message", message is null ? null : Uia.Describe(message));
            context.Record("logged", logged);
            context.Shot("corrupt-settings");
            Assert.True(context.RequireApp.IsAlive, "The app exited with a corrupt library-settings.json.");
            Assert.True(message is not null, "No recoverable message (degraded panel or toast) for a corrupt library-settings.json.");
            Visibility.AssertVisiblyPainted(context.Window, message!, "Recoverable settings message");
            Assert.True(logged, "The corrupt settings file was not logged at Warning or above.");
        });

    private static int WorkerWarnings(JourneyContext context) =>
        context.LogLines().Count(line => IsWarningOrAbove(line) &&
                                         !line.Contains("ui.thread.violation", StringComparison.Ordinal) &&
                                         (line.Contains("worker", StringComparison.OrdinalIgnoreCase) ||
                                          line.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                                          line.Contains("reader", StringComparison.OrdinalIgnoreCase)));

    private static bool IsWarningOrAbove(string line) =>
        line.Contains("\"level\":\"Warning\"", StringComparison.Ordinal) ||
        line.Contains("\"level\":\"Error\"", StringComparison.Ordinal) ||
        line.Contains("\"level\":\"Critical\"", StringComparison.Ordinal);
}
