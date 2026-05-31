using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>Code-behind for the main catalogue shell (FR-CAT-001).</summary>
public partial class CatalogueShellView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="CatalogueShellView"/>.</summary>
    public CatalogueShellView()
    {
        InitializeComponent();
    }

    private void SidebarToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.ToggleSidebar();
        }
    }

    private void FilterToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.ToggleFilterPanel();
        }
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.Filter.ClearAll();
        }
    }

    private void GridViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.Grid;
        }
    }

    private void ListViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.List;
        }
    }

    private void DirectoryViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.Catalogue.CurrentView = CatalogueView.Directory;
        }
    }
}
