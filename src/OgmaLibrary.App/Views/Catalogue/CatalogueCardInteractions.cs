using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// Card behaviour shared by the grid and the list (Sept-23 Phase 10, task 10.6): the right-click
/// menu (Add to collection ▸, Remove from collection) and dragging cards onto a Collections row.
/// Both act on the whole selection when the card is part of it, otherwise on that card alone.
/// </summary>
internal static class CatalogueCardInteractions
{
    /// <summary>The drag format carrying newline-separated book ids.</summary>
    internal static readonly DataFormat<string> BookIdsFormat =
        DataFormat.CreateStringApplicationFormat("OgmaLibrary.BookIds");

    private const double DragThreshold = 6;
    private static readonly AttachedProperty<Point?> PressPointProperty =
        AvaloniaProperty.RegisterAttached<Control, Point?>("CardPressPoint", typeof(CatalogueCardInteractions));

    /// <summary>Gives each realised card container its own context menu.</summary>
    /// <param name="list">The grid or list.</param>
    internal static void Attach(ListBox list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.ContainerPrepared += (_, e) =>
        {
            if (e.Container is ListBoxItem item && item.ContextFlyout is null)
            {
                var menu = new MenuFlyout();
                menu.Opening += (_, _) => Populate(menu, item);
                item.ContextFlyout = menu;
            }
        };
    }

    /// <summary>Remembers where the left button went down on a card, for drag detection.</summary>
    /// <param name="card">The card.</param>
    /// <param name="e">The pointer event.</param>
    internal static void OnPressed(Control card, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(card);
        card.SetValue(PressPointProperty, point.Properties.IsLeftButtonPressed ? point.Position : null);
    }

    /// <summary>Starts a drag once the pointer moves past the threshold with the left button held.</summary>
    /// <param name="card">The card.</param>
    /// <param name="e">The pointer event.</param>
    internal static void OnMoved(Control card, PointerEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(card);
        if (card.GetValue(PressPointProperty) is not { } start || !point.Properties.IsLeftButtonPressed)
        {
            card.SetValue(PressPointProperty, null);
            return;
        }

        Point delta = point.Position - start;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            return;
        }

        card.SetValue(PressPointProperty, null);
        if (card.DataContext is not BookSummaryProjection book || FindShell(card) is not { Selection: not null } shell)
        {
            return;
        }

        string[] ids = TargetIds(shell, book);
        var item = new DataTransferItem();
        item.Set(BookIdsFormat, string.Join('\n', ids));
        var transfer = new DataTransfer();
        transfer.Add(item);
        UiActions.Run(
            () => DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy),
            "catalogue.card_drag");
    }

    /// <summary>Reads the dragged book ids, or an empty list when the drag carries none.</summary>
    /// <param name="e">The drag event.</param>
    /// <returns>The book ids.</returns>
    internal static string[] ReadBookIds(DragEventArgs e) =>
        e.DataTransfer.TryGetValue(BookIdsFormat) is { Length: > 0 } value
            ? value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    private static void Populate(MenuFlyout menu, ListBoxItem item)
    {
        menu.Items.Clear();
        if (item.DataContext is not BookSummaryProjection book || FindShell(item) is not { Selection: { } selection } shell)
        {
            return;
        }

        string[] ids = TargetIds(shell, book);
        ShelfMembershipViewModel membership = selection.Membership;

        var add = new MenuItem { Header = selection.AddToShelfText };
        AutomationProperties.SetName(add, selection.AddToShelfText);
        AutomationProperties.SetAutomationId(add, "Catalogue.CardMenu.AddToShelf");
        PopulateAddToShelf(add, selection, ids, "Catalogue.CardMenu.Shelf", "catalogue.card_menu_add");
        menu.Items.Add(add);

        if (membership.ActiveManualShelf is not null)
        {
            var remove = new MenuItem { Header = selection.RemoveFromShelfText };
            AutomationProperties.SetName(remove, selection.RemoveFromShelfText);
            AutomationProperties.SetAutomationId(remove, "Catalogue.CardMenu.RemoveFromShelf");
            remove.Click += (_, _) => UiActions.Run(
                () => membership.RemoveFromActiveShelfAsync(ids),
                "catalogue.card_menu_remove");
            menu.Items.Add(remove);
        }
    }

    private static string[] TargetIds(MainShellViewModel shell, BookSummaryProjection book)
    {
        IList<BookSummaryProjection> selected = shell.Catalogue.SelectedBooks;
        return selected.Any(b => b.BookId == book.BookId)
            ? selected.Select(b => b.BookId).Distinct(StringComparer.Ordinal).ToArray()
            : [book.BookId];
    }

    /// <summary>
    /// Fills an "Add to collection" submenu with one item per manual collection, or a disabled
    /// "No collections yet" item. Shared by the card menu and the inspector overflow menu.
    /// </summary>
    /// <param name="add">The submenu to fill; its existing items are replaced.</param>
    /// <param name="selection">The catalogue selection that owns shelf membership.</param>
    /// <param name="ids">The books to add.</param>
    /// <param name="automationPrefix">AutomationId prefix for each collection item.</param>
    /// <param name="operation">The UiActions operation name for logging.</param>
    internal static void PopulateAddToShelf(
        MenuItem add,
        CatalogueSelectionViewModel selection,
        string[] ids,
        string automationPrefix,
        string operation)
    {
        add.Items.Clear();
        ShelfMembershipViewModel membership = selection.Membership;
        if (membership.ManualShelves.Count == 0)
        {
            var none = new MenuItem { Header = selection.NoShelvesText, IsEnabled = false };
            AutomationProperties.SetName(none, selection.NoShelvesText);
            add.Items.Add(none);
        }

        foreach (ShelfProjection shelf in membership.ManualShelves)
        {
            var target = new MenuItem { Header = shelf.Name };
            AutomationProperties.SetName(target, shelf.Name);
            AutomationProperties.SetAutomationId(target, $"{automationPrefix}.{shelf.ShelfId}");
            string shelfId = shelf.ShelfId;
            target.Click += (_, _) => UiActions.Run(() => membership.AddToShelfAsync(shelfId, ids), operation);
            add.Items.Add(target);
        }
    }

    /// <summary>Finds the main shell view model above a visual, if any.</summary>
    /// <param name="visual">A visual inside the catalogue shell.</param>
    /// <returns>The shell view model, or <see langword="null"/>.</returns>
    internal static MainShellViewModel? FindShell(Visual visual) =>
        visual.FindAncestorOfType<CatalogueShellView>()?.DataContext as MainShellViewModel;
}
