using Avalonia.Controls;
using OgmaLibrary.App.ViewModels.Ai;

namespace OgmaLibrary.App.Views.Ai;

/// <summary>Reading plan view.</summary>
public sealed partial class ReadingPlanView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="ReadingPlanView"/>.</summary>
    public ReadingPlanView() => InitializeComponent();

    private ReadingPlanViewModel? ViewModel => DataContext as ReadingPlanViewModel;

    private async void Generate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.GenerateAsync().ConfigureAwait(true);
        }
    }

    private async void OpenBook_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is Control { DataContext: PlanStepViewModel step })
        {
            await ViewModel.OpenBookAsync(step).ConfigureAwait(true);
        }
    }
}
