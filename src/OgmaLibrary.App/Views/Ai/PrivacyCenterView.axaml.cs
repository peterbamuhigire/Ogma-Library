using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private async void Tier_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: AiPrivacyTier tier } && ViewModel is not null)
        {
            await ViewModel.SetTierAsync(tier).ConfigureAwait(true);
        }
    }

    private async void DeleteHistory_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteHistoryAsync().ConfigureAwait(true);
        }
    }

    private async void EraseEmbeddings_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.EraseEmbeddingsAsync().ConfigureAwait(true);
        }
    }

    private async void ExportAudit_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            using var stream = new MemoryStream();
            await ViewModel.ExportAuditAsync(stream).ConfigureAwait(true);
        }
    }

    private async void ExportHistory_Click(object? sender, RoutedEventArgs e)
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
