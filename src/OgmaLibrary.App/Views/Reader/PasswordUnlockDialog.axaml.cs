using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>In-app password dialog for protected PDFs (READ-009, D-92).</summary>
public sealed partial class PasswordUnlockDialog : Window
{
    /// <summary>Initializes a new instance of <see cref="PasswordUnlockDialog"/>.</summary>
    public PasswordUnlockDialog()
    {
        InitializeComponent();
        Opened += (_, _) => PasswordBox.Focus();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Unlock();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Cancel();

    private void Unlock_Click(object? sender, RoutedEventArgs e) => Unlock();

    private void Cancel()
    {
        PasswordBox.Text = null;
        Close(null);
    }

    private void Unlock()
    {
        string text = PasswordBox.Text ?? string.Empty;
        if (text.Length == 0)
        {
            PasswordBox.Focus();
            return;
        }

        var result = new PasswordPromptResult(text.ToCharArray(), RememberBox.IsChecked == true);
        PasswordBox.Text = null;
        Close(result);
    }
}
