using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Catalogue;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// The two-step write-back consent dialog (Sept-23 Phase 11 slice 5, task 11.6). Modal, so focus
/// stays inside it; Tab cycles within the window, Escape cancels, and the safe choice takes the
/// focus at each step.
/// </summary>
public sealed partial class WriteBackDialog : Window
{
    private WriteBackDialogViewModel? _subscribed;

    /// <summary>Initializes a new instance of <see cref="WriteBackDialog"/>.</summary>
    public WriteBackDialog()
    {
        InitializeComponent();
        Opened += (_, _) => FocusSafeChoice();
        Closed += (_, _) => Detach(handleClosed: true);
        DataContextChanged += (_, _) => Attach();
    }

    private WriteBackDialogViewModel? ViewModel => DataContext as WriteBackDialogViewModel;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (ViewModel is not { IsBusy: true })
            {
                Close();
            }

            return;
        }

        base.OnKeyDown(e);
    }

    private void Attach()
    {
        Detach(handleClosed: false);
        _subscribed = ViewModel;
        if (_subscribed is not null)
        {
            _subscribed.CloseRequested += OnCloseRequested;
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void Detach(bool handleClosed)
    {
        if (_subscribed is null)
        {
            return;
        }

        _subscribed.CloseRequested -= OnCloseRequested;
        _subscribed.PropertyChanged -= OnViewModelPropertyChanged;
        if (handleClosed)
        {
            _subscribed.HandleClosed();
        }

        _subscribed = null;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WriteBackDialogViewModel.Step))
        {
            Dispatcher.UIThread.Post(FocusSafeChoice);
        }
    }

    private void FocusSafeChoice()
    {
        Button? target = ViewModel?.Step switch
        {
            WriteBackDialogStep.Review => CancelButton,
            WriteBackDialogStep.Confirm => BackButton,
            WriteBackDialogStep.Result => CloseButton,
            _ => null,
        };
        target?.Focus(NavigationMethod.Tab);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => ViewModel?.Cancel();

    private void Back_Click(object? sender, RoutedEventArgs e) => ViewModel?.Back();

    private void Continue_Click(object? sender, RoutedEventArgs e) => ViewModel?.Continue();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void Confirm_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => ViewModel?.ConfirmAsync() ?? Task.CompletedTask,
            "catalogue.write_back_dialog.confirm");

    private void Restore_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(
            () => ViewModel?.RestoreAsync() ?? Task.CompletedTask,
            "catalogue.write_back_dialog.restore");
}
