using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>Shows <see cref="PasswordUnlockDialog"/> as a modal dialog on the UI thread.</summary>
public sealed class AvaloniaPasswordPrompt : IPasswordPrompt
{
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a new instance of <see cref="AvaloniaPasswordPrompt"/>.</summary>
    public AvaloniaPasswordPrompt(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
    }

    /// <inheritdoc />
    public async Task<PasswordPromptResult?> PromptAsync(
        PasswordPromptRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: { } owner,
                })
            {
                return null;
            }

            var dialog = new PasswordUnlockDialog
            {
                DataContext = new PasswordUnlockDialogViewModel(_localization, request),
            };
            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => Dispatcher.UIThread.Post(dialog.Close));
            return await dialog.ShowDialog<PasswordPromptResult?>(owner).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }
}
