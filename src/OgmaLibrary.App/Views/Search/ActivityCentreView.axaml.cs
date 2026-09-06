using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OgmaLibrary.App.ViewModels.Search;

namespace OgmaLibrary.App.Views.Search;

/// <summary>Code-behind for file selection and item actions in the activity centre.</summary>
public partial class ActivityCentreView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ActivityCentreView"/> class.</summary>
    public ActivityCentreView()
    {
        InitializeComponent();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityCentreViewModel viewModel)
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private async void Retry_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityCentreViewModel viewModel &&
            sender is Control { DataContext: ActivityJobDisplayItem job })
        {
            await viewModel.RetryAsync(job).ConfigureAwait(true);
        }
    }

    private async void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ActivityCentreViewModel viewModel &&
            sender is Control { DataContext: ActivityJobDisplayItem job })
        {
            await viewModel.CancelAsync(job).ConfigureAwait(true);
        }
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ActivityCentreViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider.CanSave != true)
        {
            return;
        }

        IStorageFile? file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = viewModel.ExportLabel,
                SuggestedFileName = "ogma-job-diagnostics.json",
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
            await viewModel.ExportAsync(stream).ConfigureAwait(true);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
