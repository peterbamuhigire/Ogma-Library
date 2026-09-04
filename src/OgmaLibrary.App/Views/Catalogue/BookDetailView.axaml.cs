using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the book-detail slide-in panel (FR-CAT-004).</summary>
public partial class BookDetailView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="BookDetailView"/>.</summary>
    public BookDetailView()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.Close();
        }
    }

    private void ReadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            _ = vm.OpenReaderAsync();
        }
    }

    private async void EnrichButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.EnrichMetadataAsync().ConfigureAwait(true);
        }
    }

    private async void RunOcrButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.RunOcrAsync().ConfigureAwait(true);
        }
    }

    private async void ForgetPasswordButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ForgetPasswordAsync().ConfigureAwait(true);
        }
    }

    private async void ReadingMemoryField_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private async void SaveReadingMemoryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private async void LoadReadingHistoryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.LoadReadingHistoryAsync().ConfigureAwait(true);
        }
    }

    private async void LoadTocButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.LoadTocAsync().ConfigureAwait(true);
        }
    }

    private async void PrepareWriteBackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.PrepareWriteBackAsync().ConfigureAwait(true);
        }
    }

    private async void ConfirmWriteBackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ConfirmWriteBackAsync().ConfigureAwait(true);
        }
    }

    private async void RestoreWriteBackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.RestoreWriteBackAsync().ConfigureAwait(true);
        }
    }

    private void CancelWriteBackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.CancelWriteBack();
        }
    }

    private void LoadProvenanceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            vm.LoadProvenance();
        }
    }

    private async void CurationStatusButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookDetailViewModel vm || sender is not Button button ||
            !Enum.TryParse(button.Tag?.ToString(), out ReadingStatus status))
        {
            return;
        }

        await vm.SetReadingStatusAsync(status).ConfigureAwait(true);
    }

    private async void RatingButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookDetailViewModel vm || sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int rating))
        {
            return;
        }

        await vm.SetRatingAsync(rating).ConfigureAwait(true);
    }

    private async void FavouriteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ToggleFavouriteAsync().ConfigureAwait(true);
        }
    }

    private async void SaveTagsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveTagsAsync().ConfigureAwait(true);
        }
    }

    private async void AcceptMetadataProposalButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MetadataProposalItemViewModel proposal } &&
            DataContext is BookDetailViewModel vm)
        {
            await vm.AcceptMetadataProposalAsync(proposal).ConfigureAwait(true);
        }
    }

    private async void RejectMetadataProposalButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MetadataProposalItemViewModel proposal } &&
            DataContext is BookDetailViewModel vm)
        {
            await vm.RejectMetadataProposalAsync(proposal).ConfigureAwait(true);
        }
    }
}
