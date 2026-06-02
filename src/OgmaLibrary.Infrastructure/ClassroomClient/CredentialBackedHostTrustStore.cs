using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Stores Host TOFU pins in the classroom credential store.</summary>
internal sealed class CredentialBackedHostTrustStore : IHostTrustStore
{
    private const string TrustPinKeyPrefix = "ogma.classroom.hostTrust.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IClassroomCredentialStore _credentialStore;

    public CredentialBackedHostTrustStore(IClassroomCredentialStore credentialStore)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
    }

    public async Task<HostTrustPin?> GetAsync(string hostKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostKey);
        cancellationToken.ThrowIfCancellationRequested();

        string? payload = await _credentialStore.GetSecretAsync(CreateCredentialKey(hostKey), cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<HostTrustPin>(payload, JsonOptions);
    }

    public Task SaveAsync(HostTrustPin pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);
        cancellationToken.ThrowIfCancellationRequested();

        string payload = JsonSerializer.Serialize(pin, JsonOptions);
        return _credentialStore.SaveSecretAsync(CreateCredentialKey(pin.HostKey), payload, cancellationToken);
    }

    public Task DeleteAsync(string hostKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostKey);
        cancellationToken.ThrowIfCancellationRequested();
        return _credentialStore.DeleteSecretAsync(CreateCredentialKey(hostKey), cancellationToken);
    }

    internal static string CreateCredentialKey(string hostKey) => TrustPinKeyPrefix + hostKey.Trim().ToLowerInvariant();
}
