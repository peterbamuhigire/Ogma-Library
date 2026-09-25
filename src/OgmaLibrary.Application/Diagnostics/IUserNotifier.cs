namespace OgmaLibrary.Application.Diagnostics;

/// <summary>How prominently a user notification is presented.</summary>
public enum UserNotificationSeverity
{
    /// <summary>Neutral information, such as a completed background action.</summary>
    Information = 0,

    /// <summary>Something degraded but the user can continue.</summary>
    Warning = 1,

    /// <summary>An action failed; the user may retry or take another path.</summary>
    Error = 2,
}

/// <summary>The optional recovery action offered with a notification.</summary>
public enum UserNotificationActionKind
{
    /// <summary>No action beyond dismissing.</summary>
    None = 0,

    /// <summary>Repeat the failed action.</summary>
    Retry = 1,

    /// <summary>Open application settings.</summary>
    OpenSettings = 2,

    /// <summary>Save a redacted diagnostics bundle.</summary>
    ExportDiagnostics = 3,
}

/// <summary>
/// A localised, non-modal message for the user. Messages are expressed as localisation keys so
/// they follow the active culture; raw exception text is never carried (K81).
/// </summary>
/// <param name="MessageKey">Localisation key of the message body.</param>
/// <param name="Severity">Presentation severity.</param>
/// <param name="ActionKind">The optional recovery action.</param>
/// <param name="Action">The callback run when the user chooses the action.</param>
public sealed record UserNotification(
    string MessageKey,
    UserNotificationSeverity Severity = UserNotificationSeverity.Error,
    UserNotificationActionKind ActionKind = UserNotificationActionKind.None,
    Func<CancellationToken, Task>? Action = null);

/// <summary>
/// Presents non-modal, dismissible, localised notifications (the Phase 02 error surface).
/// Implementations are thread-safe and marshal onto the UI thread themselves.
/// </summary>
public interface IUserNotifier
{
    /// <summary>Shows <paramref name="notification"/> to the user.</summary>
    /// <param name="notification">The notification to present.</param>
    void Notify(UserNotification notification);
}

/// <summary>A notifier that discards every notification; used where no UI exists.</summary>
public sealed class NullUserNotifier : IUserNotifier
{
    /// <summary>The shared instance.</summary>
    public static NullUserNotifier Instance { get; } = new();

    /// <inheritdoc />
    public void Notify(UserNotification notification) => ArgumentNullException.ThrowIfNull(notification);
}
