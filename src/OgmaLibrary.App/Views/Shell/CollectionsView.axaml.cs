using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Shell;

/// <summary>The Collections destination (Sept-23 Phase 07).</summary>
public sealed partial class CollectionsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="CollectionsView"/> class.</summary>
    public CollectionsView() => AvaloniaXamlLoader.Load(this);

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
