using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// Code-behind for the grid view. Selection is handled via ListBox binding directly
/// to <see cref="ViewModels.Catalogue.CatalogueViewModel.SelectedBook"/>.
/// </summary>
public partial class CatalogueGridView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="CatalogueGridView"/>.</summary>
    public CatalogueGridView()
    {
        InitializeComponent();
    }

    private async void BookCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Control source = sender as Control ?? this;
        if (e.ClickCount != 2 ||
            e.Pointer.Type != PointerType.Mouse ||
            !e.GetCurrentPoint(source).Properties.IsLeftButtonPressed ||
            sender is not Border { DataContext: BookSummaryProjection book } ||
            this.FindAncestorOfType<CatalogueShellView>()?.DataContext is not MainShellViewModel shell)
        {
            return;
        }

        e.Handled = true;
        await shell.OpenReaderAsync(book.BookId).ConfigureAwait(true);
    }
}
