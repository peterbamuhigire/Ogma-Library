namespace OgmaLibrary.Application.Reader;

/// <summary>Provides passwords for protected PDF documents without catalogue persistence.</summary>
public interface IPasswordProvider
{
    /// <summary>Gets a password for the specified book/content hash.</summary>
    Task<PasswordResult> GetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Forgets a stored password for the specified book/content hash.</summary>
    Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Password request context.</summary>
public sealed record PasswordRequest(
    string BookId,
    string ContentHash,
    string? Title = null);

/// <summary>Password lookup result. Dispose after use to clear the password buffer.</summary>
public sealed class PasswordResult : IDisposable
{
    private PasswordResult(char[]? password, bool wasStored, bool userCancelled, string? errorMessage)
    {
        Password = password;
        WasStored = wasStored;
        UserCancelled = userCancelled;
        ErrorMessage = errorMessage;
    }

    /// <summary>Password characters. Callers must dispose this result after use.</summary>
    public char[]? Password { get; private set; }

    /// <summary>Whether the password came from or was written to OS storage.</summary>
    public bool WasStored { get; }

    /// <summary>Whether the user cancelled the prompt.</summary>
    public bool UserCancelled { get; }

    /// <summary>Optional provider error message.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates a successful result.</summary>
    public static PasswordResult Success(char[] password, bool wasStored)
    {
        ArgumentNullException.ThrowIfNull(password);
        return new PasswordResult(password, wasStored, userCancelled: false, errorMessage: null);
    }

    /// <summary>Creates a cancelled result.</summary>
    public static PasswordResult Cancelled() =>
        new(null, wasStored: false, userCancelled: true, errorMessage: null);

    /// <summary>Creates a failed result.</summary>
    public static PasswordResult Failed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new PasswordResult(null, wasStored: false, userCancelled: false, errorMessage);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Password is not null)
        {
            Array.Clear(Password);
            Password = null;
        }
    }
}
