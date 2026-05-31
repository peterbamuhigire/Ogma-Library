using Avalonia.Controls;

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
}
