using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Coordinates password unlock requests without persisting secrets in the catalogue.</summary>
public sealed class PasswordUnlockViewModel : INotifyPropertyChanged
{
    private readonly IPasswordProvider _passwordProvider;
    private readonly ILocalizationService _localization;
    private readonly IReaderSessionService? _readerSessionService;
    private readonly IPasswordPrompt? _prompt;
    private string _statusText;
    private bool _isUnlocking;

    /// <summary>Initializes a new instance of <see cref="PasswordUnlockViewModel"/>.</summary>
    public PasswordUnlockViewModel(
        IPasswordProvider passwordProvider,
        ILocalizationService localization,
        IReaderSessionService? readerSessionService = null,
        IPasswordPrompt? prompt = null)
    {
        ArgumentNullException.ThrowIfNull(passwordProvider);
        ArgumentNullException.ThrowIfNull(localization);

        _passwordProvider = passwordProvider;
        _localization = localization;
        _readerSessionService = readerSessionService;
        _prompt = prompt;
        _statusText = _localization["PasswordUnlock.Status.Locked"];
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Current unlock status.</summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether a password request is in progress.</summary>
    public bool IsUnlocking
    {
        get => _isUnlocking;
        private set
        {
            if (_isUnlocking != value)
            {
                _isUnlocking = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Requests a password and returns the disposable result to the caller.</summary>
    public async Task<PasswordResult> RequestUnlockAsync(
        string bookId,
        string contentHash,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        IsUnlocking = true;
        try
        {
            PasswordResult result = await _passwordProvider
                .GetPasswordAsync(new PasswordRequest(bookId, contentHash, title), cancellationToken)
                .ConfigureAwait(false);
            StatusText = result.UserCancelled
                ? _localization["PasswordUnlock.Status.Cancelled"]
                : result.Password is not null
                    ? _localization["PasswordUnlock.Status.Unlocked"]
                    : result.ErrorMessage ?? _localization["PasswordUnlock.Status.Locked"];
            return result;
        }
        finally
        {
            IsUnlocking = false;
        }
    }

    /// <summary>
    /// Opens a protected book: tries the keychain password first, otherwise asks in the in-app
    /// dialog. A wrong password re-asks with a wrong-password message; cancel returns
    /// <see langword="null"/> and leaves the book locked. Password buffers are always cleared.
    /// </summary>
    public async Task<ReaderSession?> RequestAndOpenAsync(
        string bookId,
        string contentHash,
        string? title = null,
        int? pageHint = null,
        CancellationToken cancellationToken = default)
    {
        if (_readerSessionService is null)
        {
            throw new InvalidOperationException("No reader session service is available for protected open.");
        }

        var request = new PasswordRequest(bookId, contentHash, title);
        IsUnlocking = true;
        try
        {
            using (PasswordResult stored = await _passwordProvider
                       .GetPasswordAsync(request, cancellationToken).ConfigureAwait(true))
            {
                if (stored.Password is not null)
                {
                    ReaderSession? session = await TryOpenAsync(bookId, pageHint, stored.Password, cancellationToken)
                        .ConfigureAwait(true);
                    if (session is not null)
                    {
                        StatusText = _localization["PasswordUnlock.Status.Unlocked"];
                        return session;
                    }

                    // The stored password no longer works (changed PDF or stale entry).
                    await _passwordProvider.ForgetPasswordAsync(request, cancellationToken).ConfigureAwait(true);
                    StatusText = _localization["PasswordUnlock.Status.Incorrect"];
                }
            }

            if (_prompt is null)
            {
                return null;
            }

            bool wrong = false;
            while (true)
            {
                using PasswordPromptResult? answer = await _prompt
                    .PromptAsync(new PasswordPromptRequest(title ?? bookId, wrong), cancellationToken)
                    .ConfigureAwait(true);
                if (answer is null)
                {
                    StatusText = _localization["PasswordUnlock.Status.Cancelled"];
                    return null;
                }

                ReaderSession? session = await TryOpenAsync(bookId, pageHint, answer.Password, cancellationToken)
                    .ConfigureAwait(true);
                if (session is not null)
                {
                    if (answer.Remember)
                    {
                        _ = await _passwordProvider
                            .StorePasswordAsync(request, answer.Password, cancellationToken)
                            .ConfigureAwait(true);
                    }

                    StatusText = _localization["PasswordUnlock.Status.Unlocked"];
                    return session;
                }

                wrong = true;
                StatusText = _localization["PasswordUnlock.Status.Incorrect"];
            }
        }
        finally
        {
            IsUnlocking = false;
        }
    }

    private async Task<ReaderSession?> TryOpenAsync(
        string bookId,
        int? pageHint,
        char[] password,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _readerSessionService!
                .OpenProtectedAsync(bookId, pageHint, password, cancellationToken)
                .ConfigureAwait(true);
        }
        catch (PdfPasswordIncorrectException)
        {
            return null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
