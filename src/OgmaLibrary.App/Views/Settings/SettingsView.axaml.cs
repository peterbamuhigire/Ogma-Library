using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Settings;

namespace OgmaLibrary.App.Views.Settings;

/// <summary>The Settings destination (Sept-23 Phase 08, K16).</summary>
public sealed partial class SettingsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SettingsView"/> class.</summary>
    public SettingsView() => AvaloniaXamlLoader.Load(this);

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    private void OpenDataFolder_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenDataFolder();

    private void OpenLogs_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenLogsFolder();

    private void Export_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => ViewModel?.ExportDiagnosticsAsync() ?? Task.CompletedTask,
            "settings.export_diagnostics");
}
