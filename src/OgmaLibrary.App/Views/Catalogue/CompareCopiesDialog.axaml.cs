using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// The Compare copies and editions dialog (Sept-23 Phase 10, task 10.9, CAT-007). Read-only; it
/// never deletes, moves or renames a file.
/// </summary>
public sealed partial class CompareCopiesDialog : Window
{
    /// <summary>Initializes a new instance of <see cref="CompareCopiesDialog"/>.</summary>
    public CompareCopiesDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
