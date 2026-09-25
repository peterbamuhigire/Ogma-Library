using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Tests.Diagnostics;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 Phase 02: the safe handler pattern, global handlers, error surface, startup failure
/// classification and single-instance guard (K31, K70, T02.1–T02.8).
/// </summary>
public sealed class Phase02SafetyNetTests : IDisposable
{
    private readonly CapturingLogger _logger = new();
    private readonly CapturingNotifier _notifier = new();

    public Phase02SafetyNetTests() => UiActions.Configure(_logger, _notifier);

    public void Dispose() => UiActions.Configure(NullLogger.Instance, NullUserNotifier.Instance);

    [Fact]
    public async Task UiActionsRunAsync_WhenActionThrows_LogsReportsAndDoesNotThrow()
    {
        bool succeeded = await UiActions.RunAsync(
            () => throw new IOException("The pipe is being closed."),
            "reader.next");

        Assert.False(succeeded);
        CapturedLogEntry entry = Assert.Single(_logger.Entries, e => e.EventId.Name == "ui.action.failed");
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<IOException>(entry.Exception);
        Assert.Contains("reader.next", entry.Message, StringComparison.Ordinal);
        UserNotification notice = Assert.Single(_notifier.Notifications);
        Assert.Equal(UiActions.ActionFailedMessageKey, notice.MessageKey);
        Assert.Equal(UserNotificationActionKind.ExportDiagnostics, notice.ActionKind);
    }

    [Fact]
    public async Task UiActionsRunAsync_AfterAnAwait_StillCatchesTheFailure()
    {
        bool succeeded = await UiActions.RunAsync(
            async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("late failure");
            },
            "catalogue.open");

        Assert.False(succeeded);
        Assert.Single(_notifier.Notifications);
    }

    [Fact]
    public async Task UiActionsRunAsync_WhenCancelled_LogsDebugWithoutNotifying()
    {
        bool succeeded = await UiActions.RunAsync(
            () => throw new OperationCanceledException(),
            "search.query");

        Assert.False(succeeded);
        Assert.Empty(_notifier.Notifications);
        Assert.Contains(_logger.Entries, e => e.EventId.Name == "ui.action.cancelled" && e.Level == LogLevel.Debug);
    }

#pragma warning disable CA2201 // These tests prove runtime-reserved fatal exceptions are never swallowed.
    [Fact]
    public async Task UiActionsRunAsync_FatalException_IsNotSwallowed() =>
        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            UiActions.RunAsync(() => throw new OutOfMemoryException(), "fatal.probe"));

    [Fact]
    public async Task AsyncRelayCommand_IsDisabledWhileRunning_AndReportsFailures()
    {
        var gate = new TaskCompletionSource();
        int runs = 0;
        var command = new AsyncRelayCommand(
            async _ =>
            {
                runs++;
                await gate.Task;
                throw new InvalidOperationException("boom");
            },
            "test.command");

        Task<bool> first = command.ExecuteAsync();
        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute(null));
        Assert.False(await command.ExecuteAsync());

        gate.SetResult();
        Assert.False(await first);
        Assert.Equal(1, runs);
        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
        Assert.Single(_notifier.Notifications);
    }

    [Fact]
    public void DispatcherHandler_NonFatal_IsHandledLoggedAndReported()
    {
        bool handled = GlobalExceptionHandlers.HandleDispatcherException(
            new InvalidOperationException("injected"),
            _logger,
            _notifier);

        Assert.True(handled);
        Assert.Contains(_logger.Entries, e => e.EventId.Name == "app.dispatcher.recovered");
        Assert.Equal(GlobalExceptionHandlers.UnexpectedErrorMessageKey, Assert.Single(_notifier.Notifications).MessageKey);
    }

    [Fact]
    public void DispatcherHandler_Fatal_IsLeftUnhandled()
    {
        bool handled = GlobalExceptionHandlers.HandleDispatcherException(
            new OutOfMemoryException(),
            _logger,
            _notifier);

        Assert.False(handled);
        Assert.Contains(_logger.Entries, e => e.EventId.Name == "app.dispatcher.fatal" && e.Level == LogLevel.Critical);
        Assert.Empty(_notifier.Notifications);
    }
#pragma warning restore CA2201

    [Fact]
    public void UnobservedTaskHandler_LogsAtError()
    {
        GlobalExceptionHandlers.HandleUnobservedTaskException(
            new AggregateException(new TimeoutException("worker")),
            _logger);

        CapturedLogEntry entry = Assert.Single(_logger.Entries);
        Assert.Equal("app.task.unobserved", entry.EventId.Name);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public void StartupFailureClassifier_MapsEachFailureClass(Exception exception, StartupFailureKind expected) =>
        Assert.Equal(expected, StartupFailureClassifier.Classify(exception));

    public static TheoryData<Exception, StartupFailureKind> FailureCases() => new()
    {
        { new OgmaConfigurationException("DataDirectory", "bad"), StartupFailureKind.Configuration },
        { new InvalidOperationException("wrapped", new SqliteException("busy", 5)), StartupFailureKind.DatabaseLocked },
        { new SqliteException("locked", 6), StartupFailureKind.DatabaseLocked },
        { new SqliteException("corrupt", 11), StartupFailureKind.DatabaseCorrupt },
        { new SqliteException("not a database", 26), StartupFailureKind.DatabaseCorrupt },
        { new InvalidDataException("backup failed integrity"), StartupFailureKind.DatabaseCorrupt },
        { new Microsoft.EntityFrameworkCore.DbUpdateException("migration"), StartupFailureKind.Migration },
        { new UnauthorizedAccessException("read-only"), StartupFailureKind.StorageUnavailable },
        { new DirectoryNotFoundException("gone"), StartupFailureKind.StorageUnavailable },
        { new AggregateException(new UnauthorizedAccessException("nested")), StartupFailureKind.StorageUnavailable },
        { new InvalidOperationException("anything else"), StartupFailureKind.Unknown },
    };

    [Fact]
    public void StartupFailureClassifier_EveryKindHasLocalisedEnglishAndFrenchCopy()
    {
        var localization = new InMemoryLocalizationService();
        foreach (StartupFailureKind kind in Enum.GetValues<StartupFailureKind>())
        {
            string key = StartupFailureClassifier.MessageKey(kind);
            localization.SetCulture("en");
            string english = localization[key];
            localization.SetCulture("fr");
            string french = localization[key];

            Assert.DoesNotContain(key, english, StringComparison.Ordinal);
            Assert.DoesNotContain(key, french, StringComparison.Ordinal);
            Assert.NotEqual(english, french);
        }

        Assert.False(StartupFailureClassifier.CanRetry(StartupFailureKind.DatabaseCorrupt));
        Assert.True(StartupFailureClassifier.CanRetry(StartupFailureKind.DatabaseLocked));
    }

    [Fact]
    public async Task CompositionFailureShell_ShowsClassifiedMessage_AndRetryRerunsComposition()
    {
        var localization = new InMemoryLocalizationService();
        int retries = 0;
        StartupShellViewModel shell = StartupShellViewModel.CreateCompositionFailure(
            localization,
            StartupFailureKind.DatabaseLocked,
            () =>
            {
                retries++;
                return Task.CompletedTask;
            });

        Assert.True(shell.IsDegraded);
        Assert.True(shell.CanRetry);
        Assert.True(shell.CanExportDiagnostics);
        var issue = Assert.Single(shell.Issues);
        Assert.Equal("database_locked", issue.Code);
        Assert.Equal(localization["Startup.Failure.DatabaseLocked"], issue.Message);

        await shell.RetryAsync();
        Assert.Equal(1, retries);

        StartupShellViewModel corrupt = StartupShellViewModel.CreateCompositionFailure(
            localization,
            StartupFailureKind.DatabaseCorrupt,
            retry: null);
        Assert.False(corrupt.CanRetry);
    }

    [Fact]
    public void NotificationCenter_KeepsAtMostThreeToasts_AndCoalescesRepeats()
    {
        var center = new NotificationCenterViewModel(new InMemoryLocalizationService(), InlineUiDispatcher.Instance);

        center.Notify(new UserNotification("Notification.ActionFailed"));
        center.Notify(new UserNotification("Notification.ActionFailed"));
        Assert.Single(center.Toasts);

        center.Notify(new UserNotification("Notification.UnexpectedError"));
        center.Notify(new UserNotification("Notification.CrashRecovered", UserNotificationSeverity.Warning));
        center.Notify(new UserNotification("Notification.DiagnosticsExportFailed"));

        Assert.Equal(NotificationCenterViewModel.MaxVisible, center.Toasts.Count);
        Assert.DoesNotContain(center.Toasts, toast => toast.Notification.MessageKey == "Notification.ActionFailed");
        Assert.All(center.Toasts, toast => Assert.DoesNotContain("Notification.", toast.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void NotificationCenter_ExportDiagnosticsAction_UsesTheDefaultExporter()
    {
        var center = new NotificationCenterViewModel(new InMemoryLocalizationService(), InlineUiDispatcher.Instance);
        int exports = 0;
        center.ExportDiagnosticsAction = _ =>
        {
            exports++;
            return Task.CompletedTask;
        };

        center.Notify(new UserNotification(
            "Notification.CrashRecovered",
            UserNotificationSeverity.Warning,
            UserNotificationActionKind.ExportDiagnostics));
        ToastViewModel toast = Assert.Single(center.Toasts);
        Assert.True(toast.HasAction);
        Assert.Equal("Export diagnostics", toast.ActionText);

        toast.ActionCommand.Execute(null);

        Assert.Equal(1, exports);
        Assert.Empty(center.Toasts);
    }

    [Fact]
    public void SingleInstanceGuard_KeyIsStablePerDataDirectory()
    {
        string first = SingleInstanceGuard.ComputeKey(@"C:\Data\Ogma");
        Assert.Equal(first, SingleInstanceGuard.ComputeKey(@"C:\Data\Ogma\"));
        Assert.NotEqual(first, SingleInstanceGuard.ComputeKey(@"C:\Data\Other"));
        Assert.Matches("^OgmaLibrary-[0-9a-f]{16}$", first);
    }

    [Fact]
    public async Task SingleInstanceGuard_SecondLaunchHandsOffToThePrimary()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "ogma-p02-instance-" + Guid.NewGuid().ToString("N"));
        var activation = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using (SingleInstanceGuard primary = SingleInstanceGuard.Acquire(dataDirectory))
            {
                Assert.True(primary.IsPrimary);
                primary.StartListening(path => activation.TrySetResult(path));

                using SingleInstanceGuard secondary = SingleInstanceGuard.Acquire(dataDirectory);
                Assert.False(secondary.IsPrimary);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Assert.True(secondary.TrySignalPrimary(@"C:\Incoming\book.pdf", TimeSpan.FromSeconds(2)));
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));

                string? path = await activation.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(@"C:\Incoming\book.pdf", path);
            }

            using SingleInstanceGuard next = SingleInstanceGuard.Acquire(dataDirectory);
            Assert.True(next.IsPrimary);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SingleInstanceGuard_WithNoPrimary_SignalFailsWithinTheTimeout()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "ogma-p02-nobody-" + Guid.NewGuid().ToString("N"));
        using SingleInstanceGuard guard = SingleInstanceGuard.Acquire(dataDirectory);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // The primary never listens, so the hand-off must give up promptly instead of hanging.
        Assert.False(guard.TrySignalPrimary(null, TimeSpan.FromMilliseconds(600)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }
}
