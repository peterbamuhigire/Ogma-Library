using System.Globalization;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.HarnessTests;

/// <summary>T01.2 / T01.3: the fixture launches and closes the real app without leaving processes behind.</summary>
[Collection(RealWindowTests.Name)]
public sealed class FixtureLifecycleTests
{
    /// <summary>T01.2 smoke: launch, reach the shell, close cleanly.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Smoke")]
    [Trait("Tag", "Harness")]
    public void Smoke_LaunchesReachesShellAndCloses() =>
        Journey.Run("Smoke", E2ESettings.Sizes[0].ToString(), JourneySupport.Standard, context =>
        {
            context.Launch();
            Shell.WaitReady(context);
            CloseResult close = context.RequireApp.Close();
            context.Record("close", close);
            Assert.True(close.ExitedCleanly, "The app did not exit within 10 s of WM_CLOSE.");
            Assert.Empty(close.OrphanedWorkers);
        });

    /// <summary>T01.3 acceptance: 20 sequential launch/close cycles leave no stray processes.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "LaunchCycles")]
    [Trait("Tag", "Harness")]
    public void TwentyLaunchCloseCycles_LeaveNoStrayProcesses() =>
        Journey.Run("LaunchCycles", E2ESettings.Sizes[0].ToString(), JourneySupport.Standard, context =>
        {
            int cycles = int.Parse(Environment.GetEnvironmentVariable("OGMA_E2E_CYCLES") ?? "20", CultureInfo.InvariantCulture);
            var closes = new List<CloseResult>();
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                using var session = new E2ESession($"cycle{cycle}");
                using OgmaApp app = OgmaApp.Launch(session);
                app.Resize(context.Size);
                CloseResult close = app.Close();
                closes.Add(close);
            }

            Thread.Sleep(1000);
            IReadOnlyList<int> strays = OgmaApp.RunningFromExeDirectory(E2ESettings.ExePath);
            context.Record("cycles", cycles);
            context.Record("exitMs", closes.Select(c => c.ExitMilliseconds).ToArray());
            context.Record("orphanedWorkers", closes.Sum(c => c.OrphanedWorkers.Count));
            context.Record("strays", strays);
            Assert.All(closes, c => Assert.True(c.ExitedCleanly, "A cycle needed a kill after WM_CLOSE."));
            Assert.Equal(0, closes.Sum(c => c.OrphanedWorkers.Count));
            Assert.Empty(strays);
        });
}
