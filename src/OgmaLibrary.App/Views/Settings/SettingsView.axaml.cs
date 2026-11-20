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

    private void OpenLibraryHealth_Click(object? sender, RoutedEventArgs e) => ViewModel?.OpenLibraryHealth?.Invoke();

    private void About_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        TopLevel? owner = TopLevel.GetTopLevel(this);
        UiActions.Run(() => ViewModel?.ShowAboutAsync(owner) ?? Task.CompletedTask, "settings.about");
    }

    private void Export_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => ViewModel?.ExportDiagnosticsAsync() ?? Task.CompletedTask,
            "settings.export_diagnostics");

    // Sept-23 Phase 14 (task 14.7): Search and AI actions. The data context of a button inside
    // the section template is the SemanticSearchSettingsViewModel.
    private static SemanticSearchSettingsViewModel? Search(object? sender) =>
        (sender as Control)?.DataContext as SemanticSearchSettingsViewModel;

    private void SearchDownload_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Search(sender)?.DownloadAsync() ?? Task.CompletedTask, "settings.search.download");

    private void SearchCancelDownload_Click(object? sender, RoutedEventArgs e) => Search(sender)?.CancelDownload();

    private void SearchRemoveModel_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Search(sender)?.RemoveModelAsync() ?? Task.CompletedTask, "settings.search.remove");

    private void SearchCheckOllama_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Search(sender)?.CheckOllamaAsync() ?? Task.CompletedTask, "settings.search.check_ollama");

    private void SearchBuildIndex_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Search(sender)?.BuildNowAsync() ?? Task.CompletedTask, "settings.search.build");

    private void SearchPause_Click(object? sender, RoutedEventArgs e) => Search(sender)?.Pause();

    private void SearchResume_Click(object? sender, RoutedEventArgs e) => Search(sender)?.Resume();

    private void SearchRequestErase_Click(object? sender, RoutedEventArgs e) => Search(sender)?.RequestErase();

    private void SearchCancelErase_Click(object? sender, RoutedEventArgs e) => Search(sender)?.CancelErase();

    private void SearchConfirmErase_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => Search(sender)?.ConfirmEraseAsync() ?? Task.CompletedTask, "settings.search.erase");
}
