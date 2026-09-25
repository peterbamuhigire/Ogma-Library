using Avalonia.Threading;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.App.Infrastructure;

/// <summary>
/// <see cref="IUiDispatcher"/> over Avalonia's <see cref="Dispatcher.UIThread"/> (Sept-23 Phase 02,
/// T02.5). It posts and never blocks the calling thread on the UI.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    /// <summary>The shared instance.</summary>
    public static AvaloniaUiDispatcher Instance { get; } = new();

    /// <inheritdoc />
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }

    /// <inheritdoc />
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.UIThread.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).GetTask();
    }
}
