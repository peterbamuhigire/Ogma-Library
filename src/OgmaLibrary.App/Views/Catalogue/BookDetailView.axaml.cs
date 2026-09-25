using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.Infrastructure;
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

    private void EnrichButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => EnrichButton_ClickAsync(sender, e), "catalogue.enrich_button_click");

    private async Task EnrichButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.EnrichMetadataAsync().ConfigureAwait(true);
        }
    }

    private void RunOcrButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RunOcrButton_ClickAsync(sender, e), "catalogue.run_ocr_button_click");

    private async Task RunOcrButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.RunOcrAsync().ConfigureAwait(true);
        }
    }

    private void ForgetPasswordButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ForgetPasswordButton_ClickAsync(sender, e), "catalogue.forget_password_button_click");

    private async Task ForgetPasswordButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ForgetPasswordAsync().ConfigureAwait(true);
        }
    }

    private void ReadingMemoryField_LostFocus(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ReadingMemoryField_LostFocusAsync(sender, e), "catalogue.reading_memory_field_lost_focus");

    private async Task ReadingMemoryField_LostFocusAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private void SaveReadingMemoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveReadingMemoryButton_ClickAsync(sender, e), "catalogue.save_reading_memory_button_click");

    private async Task SaveReadingMemoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveReadingMemoryAsync().ConfigureAwait(true);
        }
    }

    private void LoadReadingHistoryButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LoadReadingHistoryButton_ClickAsync(sender, e), "catalogue.load_reading_history_button_click");

    private async Task LoadReadingHistoryButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.LoadReadingHistoryAsync().ConfigureAwait(true);
        }
    }

    private void LoadTocButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => LoadTocButton_ClickAsync(sender, e), "catalogue.load_toc_button_click");

    private async Task LoadTocButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.LoadTocAsync().ConfigureAwait(true);
        }
    }

    private void PrepareWriteBackButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => PrepareWriteBackButton_ClickAsync(sender, e), "catalogue.prepare_write_back_button_click");

    private async Task PrepareWriteBackButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.PrepareWriteBackAsync().ConfigureAwait(true);
        }
    }

    private void ConfirmWriteBackButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => ConfirmWriteBackButton_ClickAsync(sender, e), "catalogue.confirm_write_back_button_click");

    private async Task ConfirmWriteBackButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ConfirmWriteBackAsync().ConfigureAwait(true);
        }
    }

    private void RestoreWriteBackButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RestoreWriteBackButton_ClickAsync(sender, e), "catalogue.restore_write_back_button_click");

    private async Task RestoreWriteBackButton_ClickAsync(object? sender, RoutedEventArgs e)
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

    private void OpenProviderAttributionButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => OpenProviderAttributionButton_ClickAsync(sender, e), "catalogue.open_provider_attribution_button_click");

    private async Task OpenProviderAttributionButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ProviderAttributionLink link } ||
            !Uri.TryCreate(link.Url, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host is not ("books.google.com" or "openlibrary.org"))
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            await topLevel.Launcher.LaunchUriAsync(uri).ConfigureAwait(true);
        }
    }

    private void CurationStatusButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CurationStatusButton_ClickAsync(sender, e), "catalogue.curation_status_button_click");

    private async Task CurationStatusButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookDetailViewModel vm || sender is not Button button ||
            !Enum.TryParse(button.Tag?.ToString(), out ReadingStatus status))
        {
            return;
        }

        await vm.SetReadingStatusAsync(status).ConfigureAwait(true);
    }

    private void RatingButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RatingButton_ClickAsync(sender, e), "catalogue.rating_button_click");

    private async Task RatingButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookDetailViewModel vm || sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int rating))
        {
            return;
        }

        await vm.SetRatingAsync(rating).ConfigureAwait(true);
    }

    private void FavouriteButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => FavouriteButton_ClickAsync(sender, e), "catalogue.favourite_button_click");

    private async Task FavouriteButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.ToggleFavouriteAsync().ConfigureAwait(true);
        }
    }

    private void SaveTagsButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveTagsButton_ClickAsync(sender, e), "catalogue.save_tags_button_click");

    private async Task SaveTagsButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookDetailViewModel vm)
        {
            await vm.SaveTagsAsync().ConfigureAwait(true);
        }
    }

    private void AcceptMetadataProposalButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => AcceptMetadataProposalButton_ClickAsync(sender, e), "catalogue.accept_metadata_proposal_button_click");

    private async Task AcceptMetadataProposalButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MetadataProposalItemViewModel proposal } &&
            DataContext is BookDetailViewModel vm)
        {
            await vm.AcceptMetadataProposalAsync(proposal).ConfigureAwait(true);
        }
    }

    private void RejectMetadataProposalButton_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => RejectMetadataProposalButton_ClickAsync(sender, e), "catalogue.reject_metadata_proposal_button_click");

    private async Task RejectMetadataProposalButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MetadataProposalItemViewModel proposal } &&
            DataContext is BookDetailViewModel vm)
        {
            await vm.RejectMetadataProposalAsync(proposal).ConfigureAwait(true);
        }
    }
}
