using System.Diagnostics;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.Journeys;

/// <summary>G5: close the app mid-book, relaunch, and resume within 60 s (UX-007).</summary>
[Collection(RealWindowTests.Name)]
public sealed class G05ResumeTests
{
    /// <summary>UX-007: locate and resume within 60 s.</summary>
    public const double ResumeBudgetSeconds = 60;

    /// <summary>Oracle: the same page is restored after relaunch; the elapsed time is recorded and within budget.</summary>
    [Theory]
    [MemberData(nameof(E2ESettings.SizeData), MemberType = typeof(E2ESettings))]
    [Trait("Category", "E2E")]
    [Trait("Journey", "G5")]
    [Trait("Tag", "Reader")]
    public void G5_Resume_SamePageAfterRelaunch(string size) =>
        Journey.Run("G5", size, JourneySupport.Standard, context =>
        {
            CorpusReader probe = Corpus.Oracle.Reader;
            context.Launch(Shell.SeedEnvironment(context));
            Shell.WaitReady(context);
            Shell.AddCorpusLibrary(context);
            Shell.WaitForCatalogue(context, minimum: 1);
            Shell.OpenInReader(context, probe.Title);
            while ((Shell.CurrentPage(context) ?? 0) < probe.ResumePage)
            {
                PageTurn turn = Shell.TurnPage(context);
                Assert.True(turn.Advanced, $"Could not leave page {turn.From}.");
            }

            Assert.Equal(probe.ResumePage, Shell.CurrentPage(context));
            Thread.Sleep(1500); // let the reading position persist as a user pause would
            context.Shot("before-close");

            var resume = Stopwatch.StartNew();
            context.Launch(label: "relaunch");
            Shell.WaitReady(context);
            Shell.WaitForCatalogue(context, minimum: 1, stableFor: TimeSpan.FromSeconds(2));
            Shell.OpenInReader(context, probe.Title);
            int? restored = null;
            Uia.Poll(
                () =>
                {
                    restored = Shell.CurrentPage(context);
                    return restored == probe.ResumePage;
                },
                TimeSpan.FromSeconds(15));
            context.Record("resume.elapsedSeconds", resume.Elapsed.TotalSeconds);
            context.Record("resume.restoredPage", restored);
            context.Shot("resumed");
            Assert.True(restored == probe.ResumePage, $"Reopened at page {restored?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}; expected page {probe.ResumePage} [READ-001].");
            Assert.True(resume.Elapsed.TotalSeconds <= ResumeBudgetSeconds, $"Resume took {resume.Elapsed.TotalSeconds:0.0} s (UX-007 budget 60 s).");
        });
}
