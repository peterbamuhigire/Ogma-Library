namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Asks the user for a PDF password in an in-app, non-blocking dialog (READ-009, D-92).</summary>
public interface IPasswordPrompt
{
    /// <summary>
    /// Shows the unlock dialog. Returns <see langword="null"/> when the user cancels.
    /// The caller disposes the result so the password buffer is cleared.
    /// </summary>
    Task<PasswordPromptResult?> PromptAsync(PasswordPromptRequest request, CancellationToken cancellationToken);
}

/// <summary>What the unlock dialog needs to show.</summary>
/// <param name="BookTitle">Display title of the locked book.</param>
/// <param name="WrongPassword">True when the previous attempt was rejected.</param>
public sealed record PasswordPromptRequest(string BookTitle, bool WrongPassword);

/// <summary>The user's answer. Dispose after use to clear the password.</summary>
public sealed class PasswordPromptResult : IDisposable
{
    /// <summary>Initializes a new instance of <see cref="PasswordPromptResult"/>.</summary>
    public PasswordPromptResult(char[] password, bool remember)
    {
        ArgumentNullException.ThrowIfNull(password);
        Password = password;
        Remember = remember;
    }

    /// <summary>The entered password characters.</summary>
    public char[] Password { get; }

    /// <summary>Whether the user opted in to the system keychain.</summary>
    public bool Remember { get; }

    /// <inheritdoc />
    public void Dispose() => Array.Clear(Password);
}
