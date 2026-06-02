using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>In-memory classroom credential store until platform stores are wired.</summary>
internal sealed class InMemoryClassroomCredentialStore : IClassroomCredentialStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public Task SaveSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        _secrets[key] = value;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        _secrets.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        _secrets.Remove(key);
        return Task.CompletedTask;
    }
}
