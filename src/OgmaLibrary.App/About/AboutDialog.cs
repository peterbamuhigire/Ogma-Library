using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OgmaLibrary.App.ViewModels.About;
using OgmaLibrary.App.Views.About;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.About;

/// <summary>
/// Shows the About Ogma Library dialog modally over the given window and returns keyboard
/// focus to the element that had it before the dialog opened.
/// </summary>
public static class AboutDialog
{
    /// <summary>Shows the dialog and completes when it closes.</summary>
    /// <param name="owner">The window (or any control's top level) that owns the dialog.</param>
    /// <param name="localization">The localisation service.</param>
    /// <param name="logger">The logger for link failures.</param>
    /// <returns>A task that completes when the dialog closes.</returns>
    public static async Task ShowAsync(TopLevel? owner, ILocalizationService localization, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(logger);
        if (owner is not Window ownerWindow)
        {
            return;
        }

        IInputElement? previousFocus = ownerWindow.FocusManager?.GetFocusedElement();
        AboutWindow? dialog = null;
        dialog = new AboutWindow
        {
            DataContext = new AboutViewModel(
                localization,
                new AvaloniaExternalLinkLauncher(() => dialog),
                logger),
        };

        await dialog.ShowDialog(ownerWindow).ConfigureAwait(true);

        if (previousFocus is Control { IsEffectivelyVisible: true } control)
        {
            Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Input);
        }
    }
}
