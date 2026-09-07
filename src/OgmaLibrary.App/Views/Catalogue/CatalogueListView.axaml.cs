using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the list view.</summary>
public partial class CatalogueListView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="CatalogueListView"/>.</summary>
    public CatalogueListView()
    {
        InitializeComponent();
    }

    private async void BookRow_PointerPressed(object? sender, PointerPressedEventArgs e)
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
