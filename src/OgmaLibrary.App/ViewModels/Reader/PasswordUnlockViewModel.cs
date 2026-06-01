using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Coordinates password unlock requests without persisting secrets in the catalogue.</summary>
public sealed class PasswordUnlockViewModel : INotifyPropertyChanged
{
    private readonly IPasswordProvider _passwordProvider;
    private readonly ILocalizationService _localization;
    private readonly IReaderSessionService? _readerSessionService;
    private string _statusText;
    private bool _isUnlocking;

    /// <summary>Initializes a new instance of <see cref="PasswordUnlockViewModel"/>.</summary>
    public PasswordUnlockViewModel(
        IPasswordProvider passwordProvider,
        ILocalizationService localization,
        IReaderSessionService? readerSessionService = null)
    {
        ArgumentNullException.ThrowIfNull(passwordProvider);
        ArgumentNullException.ThrowIfNull(localization);

        _passwordProvider = passwordProvider;
        _localization = localization;
        _readerSessionService = readerSessionService;
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

    /// <summary>Requests a password and opens the protected reader session.</summary>
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

        using PasswordResult result = await RequestUnlockAsync(bookId, contentHash, title, cancellationToken)
            .ConfigureAwait(false);
        if (result.Password is null)
        {
            return null;
        }

        try
        {
            return await _readerSessionService
                .OpenProtectedAsync(bookId, pageHint, result.Password, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PdfPasswordIncorrectException)
        {
            await _passwordProvider
                .ForgetPasswordAsync(new PasswordRequest(bookId, contentHash, title), cancellationToken)
                .ConfigureAwait(false);
            StatusText = _localization["PasswordUnlock.Status.Incorrect"];
            return null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
