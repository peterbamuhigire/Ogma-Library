using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.App.Views.Search;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless coverage for the privacy-safe job activity surface.</summary>
public sealed class ActivityCentreViewModelTests
{
    [AvaloniaFact]
    public async Task ActivityCentre_LoadsSafeRows_AndInvokesEligibleActions()
    {
        var runtime = new RecordingRuntime();
        using var viewModel = new ActivityCentreViewModel(runtime, new InMemoryLocalizationService());

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, viewModel.Jobs.Count);
        Assert.Contains("Queued: 1", viewModel.QueueSummary, StringComparison.Ordinal);
        Assert.Contains("Dead-letter: 1", viewModel.FailureSummary, StringComparison.Ordinal);
        ActivityJobDisplayItem failed = viewModel.Jobs.Single(job => job.JobId == 2);
        ActivityJobDisplayItem pending = viewModel.Jobs.Single(job => job.JobId == 1);
        ActivityJobDisplayItem deadLetter = viewModel.Jobs.Single(job => job.JobId == 3);
        Assert.True(failed.CanRetry);
        Assert.True(pending.CanCancel);
        Assert.False(deadLetter.CanRetry);

        await viewModel.RetryAsync(failed);
        await viewModel.CancelAsync(pending);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, runtime.RetriedJobId);
        Assert.Equal(1, runtime.CancelledJobId);
    }

    [AvaloniaFact]
    public async Task ActivityCentre_ExportsOnlyRuntimeRedactedJson()
    {
        const string sensitive = "C:\\Users\\student\\private.pdf token=secret";
        var runtime = new RecordingRuntime();
        using var viewModel = new ActivityCentreViewModel(runtime, new InMemoryLocalizationService());
        await using var stream = new MemoryStream();

        await viewModel.ExportAsync(stream);
        string exported = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(runtime.SafeExport, exported);
        Assert.DoesNotContain(sensitive, exported, StringComparison.Ordinal);
        Assert.DoesNotContain("payload", exported, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task ActivityCentre_RendersQueueAndOperatorControls()
    {
        using var viewModel = new ActivityCentreViewModel(
            new RecordingRuntime(),
            new InMemoryLocalizationService());
        await viewModel.LoadAsync();
        var window = new Window
        {
            Content = new ActivityCentreView { DataContext = viewModel },
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            string?[] labels = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text)
                .ToArray();
            Assert.Contains("Activity centre", labels);
            Assert.Contains("MetadataExtraction", labels);
            Assert.True(window.GetVisualDescendants().OfType<Button>().Count() >= 4);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RecordingRuntime : IJobRuntimeService
    {
        public string SafeExport { get; } = "{\"metrics\":{\"pendingCount\":1},\"recentJobs\":[]}";

        public long RetriedJobId { get; private set; }

        public long CancelledJobId { get; private set; }

        public Task<JobRuntimeDiagnostics> GetDiagnosticsAsync(
            int recentJobLimit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new JobRuntimeDiagnostics(
                new JobRuntimeMetrics(
                    DateTimeOffset.UtcNow,
                    PendingCount: 1,
                    RunningCount: 1,
                    CompletedCount: 0,
                    FailedCount: 1,
                    CancelledCount: 0,
                    DeadLetterCount: 1,
                    PausedCount: 0,
                    TotalAttempts: 4,
                    ActiveByJobType: new Dictionary<string, int> { ["OcrJob"] = 1 }),
                [
                    new JobRuntimeDiagnostic(1, "SearchExtraction", JobRuntimeStatus.Pending, 0, null, null, null),
                    new JobRuntimeDiagnostic(2, "MetadataExtraction", JobRuntimeStatus.Failed, 3, "io_timeout", null, DateTimeOffset.UtcNow),
                    new JobRuntimeDiagnostic(3, "Unknown", JobRuntimeStatus.DeadLetter, 1, "unsupported_job", null, DateTimeOffset.UtcNow),
                ]));

        public Task RetryFailedAsync(long jobId, CancellationToken cancellationToken = default)
        {
            RetriedJobId = jobId;
            return Task.CompletedTask;
        }

        public Task CancelPendingAsync(long jobId, CancellationToken cancellationToken = default)
        {
            CancelledJobId = jobId;
            return Task.CompletedTask;
        }

        public Task<string> ExportDiagnosticsJsonAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SafeExport);

        public Task<JobLease?> ClaimNextAsync(IReadOnlyCollection<string> jobTypes, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CompleteAsync(long jobId, string workerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RenewAsync(long jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task FailAsync(long jobId, string workerId, JobFailure failure, int maxAttempts = 3, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
