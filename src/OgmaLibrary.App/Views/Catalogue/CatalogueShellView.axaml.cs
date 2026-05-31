using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    private async void ChooseFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            var topLevel = ResolveTopLevel(sender);

            if (topLevel is not null)
            {
                await vm.ChooseFolderAsync(topLevel).ConfigureAwait(true);
            }
            else
            {
                vm.ReportChooseFolderUnavailable();
            }
        }
    }

    private async void OpenPdfButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            var topLevel = ResolveTopLevel(sender);

            if (topLevel is not null)
            {
                await vm.OpenPdfAsync(topLevel).ConfigureAwait(true);
            }
            else
            {
                vm.ReportOpenPdfUnavailable();
            }
        }
    }

    private TopLevel? ResolveTopLevel(object? sender)
    {
        if (sender is Control source && TopLevel.GetTopLevel(source) is { } senderTopLevel)
        {
            return senderTopLevel;
        }

        if (TopLevel.GetTopLevel(this) is { } viewTopLevel)
        {
            return viewTopLevel;
        }

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
