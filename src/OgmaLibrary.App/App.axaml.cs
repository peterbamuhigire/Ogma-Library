using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Ai;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.Views;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.App;

/// <summary>The Avalonia desktop application and asynchronous lifecycle boundary.</summary>
public sealed class App : Avalonia.Application, IDisposable
{
    private readonly CancellationTokenSource _applicationLifetimeCancellation = new();
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
            StartupShellViewModel startupShell = StartupShellViewModel.CreateBootstrap(localization);
            var window = new DesktopShellWindow { DataContext = startupShell };
            desktop.Exit += (_, _) => StopApplicationServices();
            desktop.MainWindow = window;

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
            ComposedRuntime runtime = await Task.Run(
                    () => ComposeRuntime(window),
                    cancellationToken)
                .ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                runtime.Services.Dispose();
                return;
            }

            _services = runtime.Services;
            window.DataContext = runtime.StartupShell;
            StartupShellViewModel startupShell = runtime.StartupShell;
            await startupShell.StartAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown during startup.
        }
        catch (Exception)
        {
            if (_services is not null)
            {
                try
                {
                    await ApplicationStartup.StopAsync(_services, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                catch (Exception)
                {
                    // Preserve the original safe failure state during best-effort cleanup.
                }

                _services.Dispose();
                _services = null;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                window.DataContext = StartupShellViewModel.CreateConfigurationFailure(
                    bootstrapLocalization,
                    "Ogma application services could not be loaded safely. No PDF files were changed. Correct the application settings and restart Ogma.");
            }
        }
    }

    private static ComposedRuntime ComposeRuntime(DesktopShellWindow ownerWindow)
    {
        OgmaRuntimeOptions options = OgmaRuntimeOptions.FromEnvironment();
        var serviceCollection = new ServiceCollection()
            .AddOgmaLibrary(options);

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
