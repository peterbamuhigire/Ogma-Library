using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OgmaLibrary.App.Ai;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.Views;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.App;

/// <summary>The Avalonia desktop application and asynchronous lifecycle boundary.</summary>
public sealed class App : Avalonia.Application, IDisposable
{
    private readonly CancellationTokenSource _applicationLifetimeCancellation = new();
    private readonly ILogger _logger = AppDiagnostics.CreateLogger<App>();
    private NotificationCenterViewModel? _notifications;
    private ServiceProvider? _services;
    private bool _disposed;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var localization = new InMemoryLocalizationService();
            NotificationCenterViewModel notifications = InstallSafetyNet(localization);
            StartupShellViewModel startupShell = StartupShellViewModel.CreateBootstrap(localization);
            var window = new DesktopShellWindow { DataContext = startupShell };
            window.AttachNotifications(notifications);
            desktop.Exit += (_, _) => StopApplicationServices();
            desktop.MainWindow = window;
            Program.InstanceGuard?.StartListening(
                _ => Dispatcher.UIThread.Post(window.BringToFront),
                _logger);
            Dispatcher.UIThread.Post(() => ShowCrashRecoveryNotice(notifications), DispatcherPriority.Background);
            ScheduleInjectedFaultForE2E();

            // Yield a frame before configuration, graph validation and view-model
            // construction. The cold-start shell must never wait for database or
            // worker preparation on the UI thread.
            Dispatcher.UIThread.Post(() =>
                _ = ComposeAndStartAsync(
                    window,
                    localization,
                    _applicationLifetimeCancellation.Token));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task ComposeAndStartAsync(
        DesktopShellWindow window,
        InMemoryLocalizationService bootstrapLocalization,
        CancellationToken cancellationToken)
    {
        try
        {
            NotificationCenterViewModel notifications = _notifications ??
                throw new InvalidOperationException("The safety net must be installed before composition.");
            ComposedRuntime ComposeRuntime() => App.ComposeRuntime(window, notifications);

            ComposedRuntime runtime = await Task.Run(ComposeRuntime, cancellationToken)
                .ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                runtime.Services.Dispose();
                return;
            }

            _services = runtime.Services;
            AppLog.CompositionCompleted(_logger);
            notifications.UseLocalization(runtime.Services.GetRequiredService<ILocalizationService>());
            _ = UpdateRedactionRootsAsync(runtime.Services, cancellationToken);
            runtime.StartupShell.MainShell?.UserPreferencesChanged +=
                (_, preferences) => ApplyUserPreferences(preferences);
            if (runtime.StartupShell.MainShell is { } mainShell)
            {
                await mainShell.InitializePreferencesAsync(cancellationToken).ConfigureAwait(true);
            }
            window.DataContext = runtime.StartupShell;
            StartupShellViewModel startupShell = runtime.StartupShell;
            await startupShell.StartAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown during startup.
        }
        catch (Exception exception) when (!ExceptionClassification.IsFatal(exception))
        {
            StartupFailureKind kind = StartupFailureClassifier.Classify(exception);
            AppLog.CompositionFailed(_logger, exception, StartupFailureClassifier.ToCode(kind));
            if (_services is not null)
            {
                try
                {
                    await ApplicationStartup.StopAsync(_services, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                catch (Exception cleanupFailure) when (!ExceptionClassification.IsFatal(cleanupFailure))
                {
                    // Preserve the original safe failure state during best-effort cleanup.
                    AppLog.CompositionCleanupFailed(_logger, cleanupFailure);
                }

                _services.Dispose();
                _services = null;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                Func<Task>? retry = StartupFailureClassifier.CanRetry(kind)
                    ? () => RetryCompositionAsync(window, bootstrapLocalization, cancellationToken)
                    : null;
                window.DataContext = StartupShellViewModel.CreateCompositionFailure(
                    bootstrapLocalization,
                    kind,
                    retry,
                    _notifications);
            }
        }
    }

    /// <summary>
    /// Reruns the whole composition (T02.8): the failure may have been a locked database, a
    /// missing folder or a transient permission problem that the user has since corrected.
    /// </summary>
    private Task RetryCompositionAsync(
        DesktopShellWindow window,
        InMemoryLocalizationService bootstrapLocalization,
        CancellationToken cancellationToken)
    {
        window.DataContext = StartupShellViewModel.CreateBootstrap(bootstrapLocalization);
        return ComposeAndStartAsync(window, bootstrapLocalization, cancellationToken);
    }

    private NotificationCenterViewModel InstallSafetyNet(ILocalizationService localization)
    {
        var notifications = new NotificationCenterViewModel(localization, AvaloniaUiDispatcher.Instance);
        notifications.ExportDiagnosticsAction = cancellationToken =>
            DiagnosticsExport.ExportAsync(notifications, cancellationToken: cancellationToken);
        _notifications = notifications;
        UiActions.Configure(AppDiagnostics.LoggerFactory.CreateLogger(typeof(UiActions).FullName!), notifications);
        UiThreadGuard.Configure(
            UiThreadGuard.ResolveMode(),
            Dispatcher.UIThread.CheckAccess,
            AppDiagnostics.LoggerFactory.CreateLogger(typeof(UiThreadGuard).FullName!));
        GlobalExceptionHandlers.InstallDispatcherHandler(Dispatcher.UIThread, notifications);
        return notifications;
    }

    private static void ShowCrashRecoveryNotice(NotificationCenterViewModel notifications)
    {
        if (AppDiagnostics.LogsDirectory is not { } logs || CrashMarker.TryConsume(logs) is null)
        {
            return;
        }

        notifications.Notify(new UserNotification(
            "Notification.CrashRecovered",
            UserNotificationSeverity.Warning,
            UserNotificationActionKind.ExportDiagnostics));
    }

    /// <summary>
    /// The hidden E2E test commands of T02.1, active only with <c>OGMA_E2E=1</c>:
    /// <c>OGMA_E2E_INJECT_UI_FAULT=1</c> throws once on the UI thread after startup to prove the
    /// dispatcher safety net keeps the app alive and logs the failure;
    /// <c>OGMA_E2E_INJECT_CRASH=1</c> throws on a background thread to prove a terminating
    /// failure is logged and leaves <c>last-crash.json</c> for the next launch's notice.
    /// </summary>
    private void ScheduleInjectedFaultForE2E()
    {
        if (!IsSet("OGMA_E2E"))
        {
            return;
        }

        if (IsSet("OGMA_E2E_INJECT_UI_FAULT"))
        {
            _ = InjectFaultAsync(crash: false);
        }

        if (IsSet("OGMA_E2E_INJECT_CRASH"))
        {
            _ = InjectFaultAsync(crash: true);
        }

        static bool IsSet(string name) =>
            string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);
    }

    private async Task InjectFaultAsync(bool crash)
    {
        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        AppLog.FaultInjected(_logger);
        if (crash)
        {
            new Thread(() => throw new InvalidOperationException("OGMA_E2E injected crash.")) { IsBackground = true }
                .Start();
            return;
        }

        Dispatcher.UIThread.Post(() => throw new InvalidOperationException("OGMA_E2E injected UI-thread fault."));
    }

    private async Task UpdateRedactionRootsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            var roots = new List<string>();
            if (services.GetService<ILibrarySettingsService>() is { } settings &&
                await settings.GetLibraryRootAsync(cancellationToken).ConfigureAwait(false) is { } root)
            {
                roots.Add(root);
            }

            // Sept-23 Phase 05: every library folder's prefix is collapsed in logs.
            if (services.GetService<ILibraryRootService>() is { } libraryRoots)
            {
                roots.AddRange((await libraryRoots.ListAsync(cancellationToken).ConfigureAwait(false))
                    .Select(descriptor => descriptor.CanonicalLocator)
                    .OfType<string>());
            }

            if (roots.Count > 0)
            {
                AppDiagnostics.Redactor.SetLibraryRoots([.. roots.Distinct(StringComparer.OrdinalIgnoreCase)]);
            }
        }
        catch (Exception exception) when (!ExceptionClassification.IsFatal(exception))
        {
            // PDF names stay hashed by the generic rule; only the root prefix is not collapsed.
            AppLog.RedactionRootsFailed(_logger, exception);
        }
    }

    private void ApplyUserPreferences(UserPreferences preferences)
    {
        RequestedThemeVariant = preferences.Theme switch
        {
            UserTheme.Dark => ThemeVariant.Dark,
            UserTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Light,
        };

        double scale = preferences.Density == UserDensity.Compact ? 0.9 : 1.0;
        if (Resources is null)
        {
            return;
        }

        foreach ((string key, double value) in new Dictionary<string, double>
        {
            ["Type.Size.Caption"] = 11,
            ["Type.Size.Small"] = 12,
            ["Type.Size.Body"] = 14,
            ["Type.Size.BodyMedium"] = 15,
            ["Type.Size.Subtitle"] = 16,
            ["Type.Size.Title"] = 20,
            ["Type.Size.Display"] = 30,
            ["Spacing.XXS"] = 4,
            ["Spacing.XS"] = 8,
            ["Spacing.S"] = 12,
            ["Spacing.M"] = 16,
            ["Spacing.L"] = 24,
            ["Spacing.XL"] = 32,
        })
        {
            Resources[key] = value * scale;
        }
    }

    private static ComposedRuntime ComposeRuntime(
        DesktopShellWindow ownerWindow,
        NotificationCenterViewModel notifications)
    {
        OgmaRuntimeOptions options = OgmaRuntimeOptions.FromEnvironment();

        // The process logging sink, error surface and UI dispatcher are bound here, in the
        // composition root only; modules fall back to null implementations when absent.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(AppDiagnostics.LoggerFactory);
        serviceCollection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        serviceCollection.AddSingleton<IUserNotifier>(notifications);
        serviceCollection.AddSingleton<IUiDispatcher>(AvaloniaUiDispatcher.Instance);
        serviceCollection.AddOgmaLibrary(options);

        // Infrastructure composition remains fail-closed for workers and tests.
        // The interactive desktop shell is the only boundary allowed to replace
        // that gate with a visible, user-controlled payload preview.
        serviceCollection.AddSingleton<IAiPreviewGate>(serviceProvider =>
            new AvaloniaPreviewGate(
                serviceProvider.GetRequiredService<ILocalizationService>(),
                () => ownerWindow));

        ServiceProvider services = serviceCollection
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        try
        {
            return new ComposedRuntime(
                services,
                services.GetRequiredService<StartupShellViewModel>());
        }
        catch
        {
            services.Dispose();
            throw;
        }
    }

    private void StopApplicationServices()
    {
        if (_disposed)
        {
            return;
        }

        _applicationLifetimeCancellation.Cancel();
        if (_services is null)
        {
            _applicationLifetimeCancellation.Dispose();
            _disposed = true;
            return;
        }

        try
        {
            ApplicationStartup.StopAsync(_services).GetAwaiter().GetResult();
        }
        finally
        {
            _services.Dispose();
            _services = null;
            _applicationLifetimeCancellation.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_applicationLifetimeCancellation.IsCancellationRequested)
        {
            _applicationLifetimeCancellation.Cancel();
        }

        _services?.Dispose();
        _services = null;
        _applicationLifetimeCancellation.Dispose();
        _disposed = true;
    }

    private sealed record ComposedRuntime(
        ServiceProvider Services,
        StartupShellViewModel StartupShell);
}
