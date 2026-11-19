using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OgmaLibrary.App.Infrastructure;
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
        if (!IsVirtualizationEnabled(Environment.GetEnvironmentVariable(VirtualizationVariable)))
        {
            BookGrid.ItemsPanel = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal });
        }

        CatalogueCardInteractions.Attach(BookGrid);
    }

    /// <summary>The administrator/test override that turns grid virtualisation off when set to <c>0</c>.</summary>
    public const string VirtualizationVariable = "OGMA_CATALOGUE_VIRTUALIZE";

    /// <summary>
    /// Whether the grid uses <see cref="VirtualizingUniformGridPanel"/>: on unless the override is <c>0</c>,
    /// which restores the former <see cref="WrapPanel"/> (Sept-23 Phase 10, D-decision).
    /// </summary>
    /// <param name="value">The value of <see cref="VirtualizationVariable"/>, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> unless the value is <c>0</c>.</returns>
    public static bool IsVirtualizationEnabled(string? value) =>
        !string.Equals(value?.Trim(), "0", StringComparison.Ordinal);

    private void BookCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control card)
        {
            CatalogueCardInteractions.OnPressed(card, e);
        }

        UiActions.Run(() => BookCard_PointerPressedAsync(sender, e), "catalogue.book_card_pointer_pressed");
    }

    private void BookCard_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Control card)
        {
            CatalogueCardInteractions.OnMoved(card, e);
        }
    }

    private async Task BookCard_PointerPressedAsync(object? sender, PointerPressedEventArgs e)
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
