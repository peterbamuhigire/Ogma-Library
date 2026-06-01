using Avalonia.Controls;
using Avalonia.Interactivity;
using OgmaLibrary.App.ViewModels.Ai;

namespace OgmaLibrary.App.Views.Ai;

/// <summary>Dialog that shows the exact AI payload before off-device egress.</summary>
public sealed partial class PayloadPreviewDialog : Window
{
    /// <summary>Initializes a new instance of <see cref="PayloadPreviewDialog"/>.</summary>
    public PayloadPreviewDialog()
    {
        InitializeComponent();
    }

    private PayloadPreviewViewModel? ViewModel => DataContext as PayloadPreviewViewModel;

    private void Send_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Send();
        Close(ViewModel?.Decision);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Cancel();
        Close(ViewModel?.Decision);
    }

    private void Remember_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.RememberForSession();
        Close(ViewModel?.Decision);
    }
}
