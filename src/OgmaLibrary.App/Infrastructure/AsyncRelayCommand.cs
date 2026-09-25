using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// An <see cref="ICommand"/> over an asynchronous action with the Phase 02 error policy
/// (Sept-23, T02.2): failures are logged and reported through <see cref="UiActions"/>, the
/// command is disabled while it runs (<see cref="IsRunning"/>) so it cannot re-enter, and an
/// optional predicate controls <see cref="CanExecute"/>.
/// </summary>
public sealed class AsyncRelayCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly string _operation;
    private bool _isRunning;

    /// <summary>Initializes the command.</summary>
    /// <param name="execute">The asynchronous action.</param>
    /// <param name="operation">A stable <c>area.action</c> name for logs.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        string operation,
        Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        _operation = operation;
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Whether the action is currently running.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    /// <inheritdoc />
    public void Execute(object? parameter) =>
        UiActions.Run(() => ExecuteAsync(CancellationToken.None), _operation);

    /// <summary>Runs the command and completes when it has finished; never throws for non-fatal failures.</summary>
    /// <param name="cancellationToken">A token to cancel the action.</param>
    /// <returns><see langword="true"/> when the action ran and succeeded.</returns>
    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExecute(null))
        {
            return false;
        }

        IsRunning = true;
        try
        {
            return await UiActions.RunAsync(() => _execute(cancellationToken), _operation)
                .ConfigureAwait(true);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Re-evaluates <see cref="CanExecute"/> for bound controls.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>A synchronous <see cref="ICommand"/> with the same error policy as <see cref="AsyncRelayCommand"/>.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    private readonly string _operation;

    /// <summary>Initializes the command.</summary>
    /// <param name="execute">The action.</param>
    /// <param name="operation">A stable <c>area.action</c> name for logs.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    public RelayCommand(Action execute, string operation, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        _operation = operation;
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        try
        {
            _execute();
        }
        catch (Exception exception) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(exception))
        {
            UiActions.Report(exception, _operation);
        }
    }

    /// <summary>Re-evaluates <see cref="CanExecute"/> for bound controls.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
