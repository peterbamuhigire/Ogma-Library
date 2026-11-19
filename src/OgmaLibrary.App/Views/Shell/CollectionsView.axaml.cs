using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.App.Views.Shell;

/// <summary>
/// The Collections destination (Sept-23 Phase 07). Since Phase 10 each row is also a drop target
/// for books dragged from the Library; the membership view model rejects smart or deleted
/// collections with a notice and writes nothing.
/// </summary>
public sealed partial class CollectionsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="CollectionsView"/> class.</summary>
    public CollectionsView()
    {
        AvaloniaXamlLoader.Load(this);
        ListBox shelfList = this.FindControl<ListBox>("ShelfList")
            ?? throw new InvalidOperationException("ShelfList is missing from CollectionsView.");
        shelfList.AddHandler(DragDrop.DragOverEvent, ShelfList_DragOver);
        shelfList.AddHandler(DragDrop.DropEvent, ShelfList_Drop);
    }

    /// <summary>The shelf row under a drag or drop event, if any.</summary>
    /// <param name="source">The event source.</param>
    /// <returns>The row's shelf, or <see langword="null"/>.</returns>
    internal static ShelfProjection? ShelfAt(object? source) =>
        (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as ShelfProjection;

    private void ShelfList_DragOver(object? sender, DragEventArgs e)
    {
        bool canDrop = ShelfAt(e.Source) is not null && CatalogueCardInteractions.ReadBookIds(e).Length > 0;
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ShelfList_Drop(object? sender, DragEventArgs e)
    {
        string[] ids = CatalogueCardInteractions.ReadBookIds(e);
        if (ShelfAt(e.Source) is not { } shelf ||
            ids.Length == 0 ||
            DataContext is not MainShellViewModel { Selection: { } selection })
        {
            return;
        }

        e.Handled = true;
        UiActions.Run(() => selection.Membership.DropOnShelfAsync(shelf, ids), "collections.drop_books");
    }

    private void CreateShelf_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => DataContext is MainShellViewModel vm ? vm.ShelfSidebar.CreateNewShelfAsync() : Task.CompletedTask,
            "collections.create_click");

    private void DeleteShelf_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => DataContext is MainShellViewModel vm ? vm.ShelfSidebar.DeleteSelectedShelfAsync() : Task.CompletedTask,
            "collections.delete_click");

    private void RenameShelf_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => DataContext is MainShellViewModel vm ? vm.ShelfSidebar.RenameSelectedShelfAsync() : Task.CompletedTask,
            "collections.rename_click");
}
