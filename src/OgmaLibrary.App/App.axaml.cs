using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views;

namespace OgmaLibrary.App;

/// <summary>
/// The Avalonia application. On framework initialization it builds the dependency
/// injection container through the single <see cref="CompositionRoot"/>, resolves the
/// main window view model, and shows the main window.
/// </summary>
public sealed class App : Avalonia.Application
{
    private IServiceProvider? _services;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        _services = new ServiceCollection().AddOgmaLibrary().BuildServiceProvider();
        ApplicationStartup.InitializeAsync(_services).GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainShellViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
