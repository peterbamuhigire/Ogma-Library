using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.App.Views.Ai;

/// <summary>Privacy Center view shell for Phase 12 AI controls.</summary>
public sealed partial class PrivacyCenterView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="PrivacyCenterView"/>.</summary>
    public PrivacyCenterView()
    {
        InitializeComponent();
    }

    private PrivacyCenterViewModel? ViewModel => DataContext as PrivacyCenterViewModel;

    private void Tier_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UiActions.Run(() => Tier_SelectionChangedAsync(sender, e), "advisor.tier_selection_changed");

    private async Task Tier_SelectionChangedAsync(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: AiPrivacyTier tier } && ViewModel is not null)
        {
            await ViewModel.SetTierAsync(tier).ConfigureAwait(true);
        }
    }

    private void DeleteHistory_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => DeleteHistory_ClickAsync(sender, e), "advisor.delete_history_click");

    private async Task DeleteHistory_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteHistoryAsync().ConfigureAwait(true);
        }
    }

    private void EraseEmbeddings_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => EraseEmbeddings_ClickAsync(sender, e), "advisor.erase_embeddings_click");

    private async Task EraseEmbeddings_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.EraseEmbeddingsAsync().ConfigureAwait(true);
        }
    }

    private void ExportAudit_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ExportAudit_ClickAsync(sender, e), "advisor.export_audit_click");

    private async Task ExportAudit_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            using var stream = new MemoryStream();
            await ViewModel.ExportAuditAsync(stream).ConfigureAwait(true);
        }
    }

    private void ExportHistory_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ExportHistory_ClickAsync(sender, e), "advisor.export_history_click");

    private async Task ExportHistory_ClickAsync(object? sender, RoutedEventArgs e)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (ViewModel is null || topLevel?.StorageProvider.CanSave != true)
        {
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = "ogma-ai-history.json",
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"],
                        MimeTypes = ["application/json"],
                    },
                ],
            }).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        Stream stream = await file.OpenWriteAsync().ConfigureAwait(false);
        try
        {
            await ViewModel.ExportHistoryAsync(stream).ConfigureAwait(true);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
