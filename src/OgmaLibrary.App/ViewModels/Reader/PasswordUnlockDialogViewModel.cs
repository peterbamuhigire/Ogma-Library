using OgmaLibrary.Application;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Localised text for the in-app PDF password dialog (READ-009).</summary>
public sealed class PasswordUnlockDialogViewModel
{
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a new instance of <see cref="PasswordUnlockDialogViewModel"/>.</summary>
    public PasswordUnlockDialogViewModel(ILocalizationService localization, PasswordPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(request);
        _localization = localization;
        BookTitle = request.BookTitle;
        WrongPassword = request.WrongPassword;
    }

    /// <summary>Title of the locked book.</summary>
    public string BookTitle { get; }

    /// <summary>Whether the previous attempt was rejected.</summary>
    public bool WrongPassword { get; }

    /// <summary>Dialog title.</summary>
    public string Title => _localization["PasswordUnlock.Dialog.Title"];

    /// <summary>Explains what is being unlocked.</summary>
    public string PromptText => string.Format(
        _localization.CurrentCulture,
        _localization["PasswordUnlock.Dialog.PromptFormat"],
        BookTitle);

    /// <summary>Label of the password box.</summary>
    public string PasswordLabel => _localization["PasswordUnlock.Dialog.PasswordLabel"];

    /// <summary>Wrong-password message; never mentions a missing file.</summary>
    public string WrongPasswordText => _localization["PasswordUnlock.Dialog.WrongPassword"];

    /// <summary>Opt-in checkbox text.</summary>
    public string RememberText => _localization["PasswordUnlock.Dialog.Remember"];

    /// <summary>Cancel button text.</summary>
    public string CancelText => _localization["PasswordUnlock.Dialog.Cancel"];

    /// <summary>Unlock button text.</summary>
    public string UnlockText => _localization["PasswordUnlock.Dialog.Unlock"];
}
