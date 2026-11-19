using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// The bulk-edit dialog (Sept-23 Phase 10, task 10.8, CAT-005). It closes with
/// <see langword="true"/> when the form was applied and <see langword="false"/> otherwise;
/// the caller performs the write through the command history.
/// </summary>
public sealed partial class BulkEditDialog : Window
{
    /// <summary>Initializes a new instance of <see cref="BulkEditDialog"/>.</summary>
    public BulkEditDialog()
    {
        InitializeComponent();
        Opened += (_, _) => this.FindControl<TextBox>("ValueBox")?.Focus();
    }

    private BulkEditViewModel? ViewModel => DataContext as BulkEditViewModel;

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        Close(ViewModel?.Apply() == true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
