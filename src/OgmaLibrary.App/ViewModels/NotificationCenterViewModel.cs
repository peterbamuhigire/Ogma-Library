using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.App.ViewModels;

/// <summary>
/// The Phase 02 error surface (Sept-23, T02.7): a stack of at most three non-modal,
/// dismissible, localised toasts. It implements <see cref="IUserNotifier"/> and marshals every
/// change onto the UI thread, so any service or handler may report from any thread.
/// </summary>
public sealed class NotificationCenterViewModel : IUserNotifier, INotifyPropertyChanged
{
    /// <summary>The maximum number of toasts shown at once.</summary>
    public const int MaxVisible = 3;

    private static readonly TimeSpan InformationLifetime = TimeSpan.FromSeconds(8);

    private readonly IUiDispatcher _dispatcher;
    private ILocalizationService _localization;

    /// <summary>Initializes the notification centre.</summary>
    /// <param name="localization">The localisation service used for toast text.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    public NotificationCenterViewModel(ILocalizationService localization, IUiDispatcher dispatcher)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The visible toasts, oldest first.</summary>
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>Whether any toast is visible.</summary>
    public bool HasToasts => Toasts.Count > 0;

    /// <summary>
    /// The default action for <see cref="UserNotificationActionKind.ExportDiagnostics"/> when a
    /// notification does not carry its own callback.
    /// </summary>
    public Func<CancellationToken, Task>? ExportDiagnosticsAction { get; set; }

    /// <summary>Accessible name of the notification region.</summary>
    public string RegionName => _localization["Notification.Region"];

    /// <summary>
    /// Switches to the runtime localisation service once composition has finished, so toast
    /// text follows the user's language choice.
    /// </summary>
    /// <param name="localization">The runtime localisation service.</param>
    public void UseLocalization(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _dispatcher.Post(() =>
        {
            _localization.CultureChanged -= OnCultureChanged;
            _localization = localization;
            _localization.CultureChanged += OnCultureChanged;
            OnCultureChanged(this, EventArgs.Empty);
        });
    }

    /// <inheritdoc />
    public void Notify(UserNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (_dispatcher.CheckAccess())
        {
            Add(notification);
        }
        else
        {
            _dispatcher.Post(() => Add(notification));
        }
    }

    internal void Dismiss(ToastViewModel toast)
    {
        if (Toasts.Remove(toast))
        {
            OnPropertyChanged(nameof(HasToasts));
        }
    }

    private void Add(UserNotification notification)
    {
        // A repeated failure refreshes the existing toast instead of flooding the stack.
        ToastViewModel? existing = Toasts.FirstOrDefault(toast =>
            string.Equals(toast.Notification.MessageKey, notification.MessageKey, StringComparison.Ordinal));
        if (existing is not null)
        {
            Toasts.Remove(existing);
        }

        while (Toasts.Count >= MaxVisible)
        {
            Toasts.RemoveAt(0);
        }

        var toast = new ToastViewModel(this, notification, _localization);
        Toasts.Add(toast);
        OnPropertyChanged(nameof(HasToasts));
        if (notification.Severity == UserNotificationSeverity.Information &&
            notification.ActionKind == UserNotificationActionKind.None)
        {
            _ = DismissLaterAsync(toast);
        }
    }

    private async Task DismissLaterAsync(ToastViewModel toast)
    {
        await Task.Delay(InformationLifetime).ConfigureAwait(false);
        _dispatcher.Post(() => Dismiss(toast));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (ToastViewModel toast in Toasts)
        {
            toast.Relocalize(_localization);
        }

        OnPropertyChanged(nameof(RegionName));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>One dismissible toast in <see cref="NotificationCenterViewModel"/>.</summary>
public sealed class ToastViewModel : INotifyPropertyChanged
{
    private readonly NotificationCenterViewModel _owner;
    private ILocalizationService _localization;

    internal ToastViewModel(
        NotificationCenterViewModel owner,
        UserNotification notification,
        ILocalizationService localization)
    {
        _owner = owner;
        Notification = notification;
        _localization = localization;
        DismissCommand = new RelayCommand(() => _owner.Dismiss(this), "notification.dismiss");
        ActionCommand = new AsyncRelayCommand(RunActionAsync, "notification.action");
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The underlying notification.</summary>
    public UserNotification Notification { get; }

    /// <summary>The localised message.</summary>
    public string Message => _localization[Notification.MessageKey];

    /// <summary>Whether the toast reports an error.</summary>
    public bool IsError => Notification.Severity == UserNotificationSeverity.Error;

    /// <summary>Whether the toast offers a recovery action.</summary>
    public bool HasAction => ResolveAction() is not null;

    /// <summary>The localised action label.</summary>
    public string ActionText => Notification.ActionKind switch
    {
        UserNotificationActionKind.Retry => _localization["Notification.Action.Retry"],
        UserNotificationActionKind.OpenSettings => _localization["Notification.Action.OpenSettings"],
        UserNotificationActionKind.ExportDiagnostics => _localization["Notification.Action.ExportDiagnostics"],
        _ => string.Empty,
    };

    /// <summary>The localised dismiss label.</summary>
    public string DismissText => _localization["Notification.Action.Dismiss"];

    /// <summary>Dismisses the toast.</summary>
    public ICommand DismissCommand { get; }

    /// <summary>Runs the recovery action, then dismisses the toast.</summary>
    public ICommand ActionCommand { get; }

    internal void Relocalize(ILocalizationService localization)
    {
        _localization = localization;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DismissText)));
    }

    private async Task RunActionAsync(CancellationToken cancellationToken)
    {
        _owner.Dismiss(this);
        if (ResolveAction() is { } action)
        {
            await action(cancellationToken).ConfigureAwait(true);
        }
    }

    private Func<CancellationToken, Task>? ResolveAction() => Notification.ActionKind switch
    {
        UserNotificationActionKind.None => null,
        UserNotificationActionKind.ExportDiagnostics => Notification.Action ?? _owner.ExportDiagnosticsAction,
        _ => Notification.Action,
    };
}
