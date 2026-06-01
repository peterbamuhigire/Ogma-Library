using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>Password provider used when no OS-native provider is available.</summary>
public sealed class UnsupportedPasswordProvider : IPasswordProvider
{
    /// <inheritdoc />
    public Task<PasswordResult> GetPasswordAsync(
        PasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(PasswordResult.Failed("Password-protected PDF unlock is not available on this platform yet."));
    }

    /// <inheritdoc />
    public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.CompletedTask;
    }
}
