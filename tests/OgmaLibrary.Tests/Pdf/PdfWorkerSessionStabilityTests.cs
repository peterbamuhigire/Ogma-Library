using System.Diagnostics;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Session;
using PdfSharp.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit.Abstractions;

namespace OgmaLibrary.Tests.Pdf;

/// <summary>
/// Sept-23 Kaizen Phase 04 (K30/K32): the persistent reader worker session must
/// survive long reading sessions, keep its protocol correlated, recover from worker
/// loss and keep its containment limits. These tests drive the real isolated worker.
/// </summary>
public sealed class PdfWorkerSessionStabilityTests : IDisposable
{
    private const string Password = "ogma-session-password";

    private readonly ITestOutputHelper _output;
    private readonly string _root;
    private readonly string _sandboxRoot;

    public PdfWorkerSessionStabilityTests(ITestOutputHelper output)
    {
        _output = output;
        _root = Path.Combine(Path.GetTempPath(), $"ogma-session-stability-{Guid.NewGuid():N}");
        _sandboxRoot = Path.Combine(_root, "sandbox");
        Directory.CreateDirectory(_sandboxRoot);
    }

    /// <summary>
    /// T04.1 deterministic reproduction. A one-shot CPU limit of one second is far below
    /// the CPU a reading session accumulates; before Phase 04 the worker was killed by its
    /// Job Object and the next request threw ("The PDF worker session ended without a
    /// response" / "The pipe is being closed"). Sessions must not apply that limit.
    /// </summary>
    [Fact]
    public async Task PdfWorkerSession_LongReadingSession_IsNotKilledByOneShotCpuLimit()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "long-session.pdf"), pageCount: 40);
        PdfWorkerClient client = CreateClient(options => options.CpuTimeLimit = TimeSpan.FromSeconds(1));

        using PdfWorkerClient.PdfWorkerSession session = client.OpenSession(pdfPath);
        using Process worker = Process.GetProcessById(session.ProcessId);
        TimeSpan cpuBudgetToExceed = TimeSpan.FromSeconds(2.5);
        TimeSpan cpuAtOpen = SafeCpu(worker);
        int renders = 0;
        var stopwatch = Stopwatch.StartNew();

        while ((renders < 40 || SafeCpu(worker) < cpuBudgetToExceed) && stopwatch.Elapsed < TimeSpan.FromSeconds(90))
        {
            int page = renders % session.PageCount;
            int rotation = await session.GetPageRotationDegreesAsync(page, CancellationToken.None);
            PdfPageGeometry geometry = await session.GetPageGeometryAsync(page, CancellationToken.None);
            RenderResult result = await session.RenderPageAsync(page, new RenderRequest(1440), CancellationToken.None);
            Assert.NotEmpty(result.PngBytes);
            Assert.Equal(0, rotation);
            Assert.False(geometry.IsFallback);
            renders++;
        }

        TimeSpan cpu = SafeCpu(worker);
        _output.WriteLine(
            $"renders={renders} workerCpuAtOpen={cpuAtOpen.TotalMilliseconds:F0}ms workerCpu={cpu.TotalMilliseconds:F0}ms " +
            $"cpuPerRenderTurn={(cpu - cpuAtOpen).TotalMilliseconds / Math.Max(1, renders):F1}ms wall={stopwatch.ElapsedMilliseconds}ms");
        Assert.True(cpu >= cpuBudgetToExceed, "The worker should have accumulated more CPU than the one-shot limit.");
        Assert.False(worker.HasExited, "The reader worker must survive cumulative CPU use beyond the one-shot limit.");
    }

    /// <summary>T04.2: the session Job Object keeps memory, active-process and kill-on-close.</summary>
    [Fact]
    public void PdfWorkerSession_JobObject_KeepsContainmentWithoutCumulativeCpuLimit()
    {
        if (!OperatingSystem.IsWindows())
        {
            _output.WriteLine("NOT ASSESSED: Job Objects are Windows-only (macOS limits are Phase 27).");
            return;
        }

        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "limits.pdf"), pageCount: 1);
        PdfWorkerClient client = CreateClient();

        using PdfWorkerClient.PdfWorkerSession session = client.OpenSession(pdfPath);
        JobLimitSnapshot? limits = session.AppliedLimits;

        Assert.NotNull(limits);
        Assert.True(limits.KillOnJobClose);
        Assert.Equal(1, limits.ActiveProcessLimit);
        Assert.Equal(new PdfWorkerOptions().MaxMemoryBytes, limits.ProcessMemoryLimitBytes);
        Assert.Null(limits.CpuTimeLimit);
    }

    /// <summary>T04.2: one-shot jobs keep the cumulative CPU limit alongside the other limits.</summary>
    [Fact]
    public void WindowsChildProcessLimit_OneShotPolicy_KeepsCpuLimit()
    {
        if (!OperatingSystem.IsWindows())
        {
            _output.WriteLine("NOT ASSESSED: Job Objects are Windows-only.");
            return;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            },
        };
        process.Start();
        try
        {
            using WindowsChildProcessLimit? limit = WindowsChildProcessLimit.TryAssign(
                process,
                256L * 1024 * 1024,
                TimeSpan.FromSeconds(15));
            JobLimitSnapshot? limits = limit?.QueryLimits();

            Assert.NotNull(limits);
            Assert.True(limits.KillOnJobClose);
            Assert.Equal(1, limits.ActiveProcessLimit);
            Assert.Equal(256L * 1024 * 1024, limits.ProcessMemoryLimitBytes);
            Assert.Equal(TimeSpan.FromSeconds(15), limits.CpuTimeLimit);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    /// <summary>
    /// T04.3/T04.4: interleaved renders and geometry requests each receive their own
    /// response, and geometry is scheduled ahead of queued renders.
    /// </summary>
    [Fact]
    public async Task PdfWorkerSession_ConcurrentRequests_AreCorrelatedAndGeometryJumpsTheQueue()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "mixed.pdf"), pageCount: 12, alternateSizes: true);
        PdfWorkerClient client = CreateClient();
        using PdfWorkerClient.PdfWorkerSession session = client.OpenSession(pdfPath);

        // The first geometry call parses the page tree once per worker (the reader does
        // this when a book opens); measure the steady state behind queued renders.
        var warmup = Stopwatch.StartNew();
        await session.GetPageGeometryAsync(0, CancellationToken.None);
        _output.WriteLine($"firstGeometryMs={warmup.ElapsedMilliseconds}");

        var completionOrder = new List<string>();
        async Task<RenderResult> Render(int page)
        {
            RenderResult result = await session.RenderPageAsync(page, new RenderRequest(1600), CancellationToken.None);
            lock (completionOrder)
            {
                completionOrder.Add($"render-{page}");
            }

            return result;
        }

        Task<RenderResult>[] renders = [.. Enumerable.Range(0, 6).Select(Render)];
        await Task.Delay(20);
        var stopwatch = Stopwatch.StartNew();
        PdfPageGeometry single = await session.GetPageGeometryAsync(5, CancellationToken.None);
        long geometryMs = stopwatch.ElapsedMilliseconds;
        PdfPageGeometry[] geometries = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(page => session.GetPageGeometryAsync(page, CancellationToken.None)));
        Assert.Equal(5, single.PageIndex);
        Assert.True(geometryMs < 2_000, $"Geometry behind queued renders took {geometryMs} ms.");
        lock (completionOrder)
        {
            completionOrder.Add("geometry");
        }

        RenderResult[] results = await Task.WhenAll(renders);
        _output.WriteLine($"geometryBehindSixQueuedRendersMs={geometryMs} order={string.Join(",", completionOrder)}");

        for (int page = 0; page < 12; page++)
        {
            Assert.Equal(page, geometries[page].PageIndex);
            Assert.Equal(StabilityPdfFixture.ExpectedWidthPoints(page), geometries[page].WidthPoints, precision: 0);
        }

        for (int page = 0; page < 6; page++)
        {
            Assert.Equal(page, results[page].PageIndex);
            Assert.Equal(StabilityPdfFixture.ExpectedWidthPoints(page), results[page].PageWidthPoints, precision: 0);
        }

        Assert.True(
            completionOrder.IndexOf("geometry") < completionOrder.Count - 1,
            "Geometry must be answered before the queued renders drain.");
        Assert.Equal(0, session.DiscardedResponses);
    }

    /// <summary>T04.2: a request exceeding its wall clock kills the worker (runaway guard).</summary>
    [Fact]
    public async Task PdfWorkerSession_RequestTimeout_KillsWorkerInsteadOfDesynchronising()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "timeout.pdf"), pageCount: 2);
        PdfWorkerClient client = CreateClient(options => options.Session.RequestTimeout = TimeSpan.FromMilliseconds(1));
        using PdfWorkerClient.PdfWorkerSession session = client.OpenSession(pdfPath);
        using Process worker = Process.GetProcessById(session.ProcessId);

        PdfWorkerSessionLostException lost = await Assert.ThrowsAsync<PdfWorkerSessionLostException>(
            () => session.RenderPageAsync(0, new RenderRequest(2400), CancellationToken.None));

        Assert.Equal("request_timeout", lost.Reason);
        Assert.True(worker.WaitForExit(5_000), "The runaway worker must be killed.");
        Assert.True(session.IsFaulted);
        await Assert.ThrowsAsync<PdfWorkerSessionLostException>(
            () => session.GetPageGeometryAsync(1, CancellationToken.None));
    }

    /// <summary>T04.5 / G8: killing the worker mid-read is recovered on the next page turn.</summary>
    [Fact]
    public async Task IsolatedPdfRenderer_WorkerKilledMidRead_NextPageRendersWithinTwoSeconds()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "kill.pdf"), pageCount: 6);
        PdfWorkerClient client = CreateClient();
        using var renderer = (IsolatedPdfRenderer)new IsolatedPdfRendererFactory(client).Open(pdfPath);
        var recoveries = new List<PdfRendererRecoveredEventArgs>();
        renderer.Recovered += (_, e) => recoveries.Add(e);

        Assert.NotEmpty((await renderer.RenderPageAsync(0, new RenderRequest(900), CancellationToken.None)).PngBytes);
        int firstPid = renderer.Supervisor.CurrentSession!.ProcessId;
        KillWorker(firstPid);

        var stopwatch = Stopwatch.StartNew();
        RenderResult next = await renderer.RenderPageAsync(1, new RenderRequest(900), CancellationToken.None);
        stopwatch.Stop();
        _output.WriteLine($"recoveryToRenderedPageMs={stopwatch.ElapsedMilliseconds}");

        Assert.Equal(1, next.PageIndex);
        Assert.NotEmpty(next.PngBytes);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Recovery took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.NotEqual(firstPid, renderer.Supervisor.CurrentSession!.ProcessId);
        PdfRendererRecoveredEventArgs recovery = Assert.Single(recoveries);
        Assert.Equal(1, recovery.RespawnCount);
        Assert.True(
            recovery.Reason is "worker_exited" or "pipe_broken",
            $"Unexpected recovery reason {recovery.Reason}.");
        Assert.Equal(1, renderer.GetHealthSnapshot().RespawnCount);
    }

    /// <summary>T04.5: a password-protected document is reopened with the retained password copy.</summary>
    [Fact]
    public async Task IsolatedPdfRenderer_PasswordProtectedSession_RespawnsWithPassword()
    {
        string pdfPath = Path.Combine(_root, "protected.pdf");
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.SecuritySettings.UserPassword = Password;
            document.SecuritySettings.OwnerPassword = "ogma-owner";
            document.Save(pdfPath);
        }

        PdfWorkerClient client = CreateClient();
        char[] password = Password.ToCharArray();
        using var renderer = (IsolatedPdfRenderer)new IsolatedPdfRendererFactory(client).Open(pdfPath, password);
        Array.Clear(password);

        KillWorker(renderer.Supervisor.CurrentSession!.ProcessId);
        RenderResult result = await renderer.RenderPageAsync(0, new RenderRequest(600), CancellationToken.None);

        Assert.NotEmpty(result.PngBytes);
        Assert.Equal(1, renderer.GetHealthSnapshot().RespawnCount);
    }

    /// <summary>T04.5: repeated worker loss stops the engine with a typed, catchable error.</summary>
    [Fact]
    public async Task IsolatedPdfRenderer_RespawnBudgetExhausted_ThrowsRendererUnavailable()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "budget.pdf"), pageCount: 2);
        PdfWorkerClient client = CreateClient(options => options.Session.MaxRespawns = 2);
        using var renderer = (IsolatedPdfRenderer)new IsolatedPdfRendererFactory(client).Open(pdfPath);

        for (int kill = 0; kill < 2; kill++)
        {
            KillWorker(renderer.Supervisor.CurrentSession!.ProcessId);
            Assert.NotEmpty((await renderer.RenderPageAsync(0, new RenderRequest(300), CancellationToken.None)).PngBytes);
        }

        KillWorker(renderer.Supervisor.CurrentSession!.ProcessId);
        await Assert.ThrowsAsync<PdfRendererUnavailableException>(
            () => renderer.RenderPageAsync(1, new RenderRequest(300), CancellationToken.None));
        Assert.Equal("Failed", renderer.GetHealthSnapshot().State);
        await Assert.ThrowsAsync<PdfRendererUnavailableException>(
            () => renderer.GetPageGeometryAsync(1, CancellationToken.None));
    }

    /// <summary>T04.2: an idle worker is closed and restarted on demand without counting as a failure.</summary>
    [Fact]
    public async Task IsolatedPdfRenderer_IdleWorker_IsClosedAndTransparentlyRestarted()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "idle.pdf"), pageCount: 2);
        PdfWorkerClient client = CreateClient(options => options.Session.IdleTimeout = TimeSpan.FromMilliseconds(400));
        using var renderer = (IsolatedPdfRenderer)new IsolatedPdfRendererFactory(client).Open(pdfPath);
        using Process worker = Process.GetProcessById(renderer.Supervisor.CurrentSession!.ProcessId);

        Assert.True(worker.WaitForExit(10_000), "The idle worker should be closed.");
        RenderResult result = await renderer.RenderPageAsync(1, new RenderRequest(300), CancellationToken.None);

        Assert.NotEmpty(result.PngBytes);
        PdfRendererHealthSnapshot health = renderer.GetHealthSnapshot();
        Assert.True(health.IdleCloseCount >= 1);
        Assert.Equal(0, health.RespawnCount);
    }

    /// <summary>T04.7: repeated open/close cycles leave no worker process behind.</summary>
    [Fact]
    public async Task IsolatedPdfRenderer_FiftyOpenCloseCycles_LeaveNoWorkers()
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, "cycles.pdf"), pageCount: 2);
        PdfWorkerClient client = CreateClient();
        var factory = new IsolatedPdfRendererFactory(client);
        var processIds = new List<int>();

        for (int cycle = 0; cycle < 50; cycle++)
        {
            var renderer = (IsolatedPdfRenderer)factory.Open(pdfPath);
            processIds.Add(renderer.Supervisor.CurrentSession!.ProcessId);
            if (cycle % 10 == 0)
            {
                Assert.NotEmpty((await renderer.RenderPageAsync(0, new RenderRequest(200), CancellationToken.None)).PngBytes);
            }

            renderer.Dispose();
        }

        int alive = processIds.Count(IsWorkerAlive);
        Assert.Equal(0, alive);
    }

    /// <summary>
    /// Fast-suite soak (G3 core, 100 turns): the reader session service over the real
    /// isolated worker turns pages without an exception, with prefetch, zoom-width changes
    /// and geometry on every turn.
    /// </summary>
    [Fact]
    public Task ReaderSession_HundredPageTurns_AllRenderWithoutFailure() => RunSoakAsync(turns: 100, pageCount: 120);

    /// <summary>
    /// Nightly soak: 1,000 turns (override with OGMA_E2E_SOAK_TURNS) on a 900-page document.
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public Task ReaderSession_ThousandPageTurnSoak_NoFailures()
    {
        int turns = int.TryParse(Environment.GetEnvironmentVariable("OGMA_E2E_SOAK_TURNS"), out int configured) && configured > 0
            ? configured
            : 1_000;
        return RunSoakAsync(turns, pageCount: 900);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary fixture root.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of the temporary fixture root.
        }
    }

    private async Task RunSoakAsync(int turns, int pageCount)
    {
        string pdfPath = StabilityPdfFixture.Create(Path.Combine(_root, $"soak-{pageCount}.pdf"), pageCount);
        PdfWorkerClient client = CreateClient();
        var factory = new IsolatedPdfRendererFactory(client);
        using var cache = new PageRenderCache(factory, new StopwatchBenchmarkContext());
        using var service = new ReaderSessionService(
            factory,
            new InMemoryProgressService(),
            new FixedBookFileLocator("soak-book", pdfPath),
            cache);

        using IDisposable events = service.Events.Subscribe(new SoakEventLog(_output));
        ReaderSession session = await service.OpenAsync("soak-book", 0, CancellationToken.None);
        Assert.Equal(pageCount, session.PageCount);
        var firstImage = new List<double>(turns);
        var sharpImage = new List<double>(turns);
        int[] widths = [1024, 1408, 1920];
        int page = 0;

        for (int turn = 0; turn < turns; turn++)
        {
            int width = widths[(turn / 25) % widths.Length];
            service.UpdateRenderWidth(width);
            page = turn % 97 == 96 ? Random.Shared.Next(pageCount) : Math.Min(pageCount - 1, page + 1);
            if (page == pageCount - 1)
            {
                page = 0;
            }

            var stopwatch = Stopwatch.StartNew();
            await service.NavigateToAsync(page);
            PdfPageGeometry geometry = await service.CurrentRenderer!.GetPageGeometryAsync(page, CancellationToken.None);
            RenderResult first = await cache.GetOrRenderAsync("soak-book", page, new RenderRequest(width), CancellationToken.None);
            firstImage.Add(stopwatch.Elapsed.TotalMilliseconds);
            RenderResult sharp = await cache.GetOrRenderAsync("soak-book", page, new RenderRequest(width), CancellationToken.None);
            sharpImage.Add(stopwatch.Elapsed.TotalMilliseconds);

            Assert.Equal(page, geometry.PageIndex);
            Assert.Equal(page, first.PageIndex);
            Assert.Equal(page, sharp.PageIndex);
            Assert.Equal(page, service.CurrentSession!.CurrentPageIndex);
        }

        var health = ((IPdfRendererHealth)service.CurrentRenderer!).GetHealthSnapshot();
        _output.WriteLine(
            $"turns={turns} pages={pageCount} first p50={Percentile(firstImage, 50):F0}ms p95={Percentile(firstImage, 95):F0}ms " +
            $"max={firstImage.Max():F0}ms | sharp p50={Percentile(sharpImage, 50):F0}ms p95={Percentile(sharpImage, 95):F0}ms " +
            $"max={sharpImage.Max():F0}ms | respawns={health.RespawnCount} discarded={health.DiscardedResponses} " +
            $"cacheBytes={cache.MemoryUsageBytes}");
        Assert.Equal(0, health.RespawnCount);
        Assert.Equal("Healthy", health.State);
        Assert.True(cache.MemoryUsageBytes <= PageRenderCache.DefaultMemoryBudgetBytes);

        await service.CloseAsync(CancellationToken.None);
    }

    private PdfWorkerClient CreateClient(Action<PdfWorkerOptions>? configure = null)
    {
        var options = new PdfWorkerOptions
        {
            SandboxRoot = _sandboxRoot,
            Timeout = TimeSpan.FromSeconds(20),
        };
        configure?.Invoke(options);
        return new PdfWorkerClient(options);
    }

    private static void KillWorker(int processId)
    {
        using Process process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
        Assert.True(process.WaitForExit(5_000));
    }

    private static bool IsWorkerAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited &&
                   process.ProcessName.Contains("OgmaLibrary.Workers", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static double Percentile(List<double> values, int percentile)
    {
        var sorted = values.OrderBy(value => value).ToList();
        int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static TimeSpan SafeCpu(Process process)
    {
        try
        {
            process.Refresh();
            return process.HasExited ? TimeSpan.MaxValue : process.TotalProcessorTime;
        }
        catch (InvalidOperationException)
        {
            return TimeSpan.MaxValue;
        }
    }

    private sealed class SoakEventLog(ITestOutputHelper output) : IObserver<ReaderEvent>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(ReaderEvent value)
        {
            if (value is ReaderEvent.EngineRecovered recovered)
            {
                output.WriteLine(
                    $"reader.session.respawned reason={recovered.Reason} count={recovered.RespawnCount} " +
                    $"elapsedMs={recovered.Elapsed.TotalMilliseconds:F0}");
            }
        }
    }

    private sealed class InMemoryProgressService : IReadingProgressService
    {
        public Task<ReaderProgress> LoadAsync(string bookId, CancellationToken ct) => Task.FromResult(ReaderProgress.Default);

        public Task SaveImmediateAsync(string bookId, ReaderProgress progress, CancellationToken ct) => Task.CompletedTask;

        public void ScheduleSave(string bookId, ReaderProgress progress)
        {
        }
    }

    private sealed class FixedBookFileLocator(string bookId, string path) : IBookFileLocator
    {
        public Task<string?> LocateAsync(string requestedBookId, CancellationToken ct) =>
            Task.FromResult<string?>(requestedBookId == bookId ? path : null);
    }
}

/// <summary>Builds deterministic text-heavy multi-page PDFs for reader stability tests.</summary>
internal static class StabilityPdfFixture
{
    public static double ExpectedWidthPoints(int pageIndex) =>
        pageIndex % 2 == 0 ? PageSizes.A4.Width : PageSizes.A5.Landscape().Width;

    public static string Create(string path, int pageCount, bool alternateSizes = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            return path;
        }

        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(container =>
        {
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                int pageNumber = pageIndex + 1;
                bool alternate = alternateSizes && pageIndex % 2 == 1;
                container.Page(page =>
                {
                    page.Size(alternate ? PageSizes.A5.Landscape() : PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(style => style.FontSize(9));
                    page.Content().Column(column =>
                    {
                        int lines = alternate ? 25 : 70;
                        for (int line = 0; line < lines; line++)
                        {
                            column.Item().Text(
                                $"Page {pageNumber} line {line + 1}: the quick brown fox jumps over the lazy dog 0123456789.");
                        }
                    });
                });
            }
        }).GeneratePdf(path);
        return path;
    }
}
