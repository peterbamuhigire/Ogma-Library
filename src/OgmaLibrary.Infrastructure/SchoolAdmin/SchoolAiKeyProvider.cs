using System.Globalization;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.SchoolAdmin;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>Stores school AI provider keys in the Host credential store.</summary>
internal sealed class SchoolAiKeyProvider : ISchoolAiKeyProvider
{
    private const string CredentialKeyPrefix = "ogma.school.ai.key.";
    private const string StoredValuePrefix = "ogma-school-ai-key-v1";
    private readonly IClassroomCredentialStore _credentialStore;

    public SchoolAiKeyProvider(IClassroomCredentialStore credentialStore)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
    }

    public async Task SaveKeyAsync(
        string providerId,
        char[] key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string normalizedProviderId = NormalizeProviderId(providerId);
            string keyValue = new(key);
            ValidateKeyValue(keyValue);
            string storedValue = CreateStoredValue(DateTimeOffset.UtcNow, keyValue);
            await _credentialStore
                .SaveSecretAsync(CreateCredentialKey(normalizedProviderId), storedValue, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(key);
        }
    }

    public async Task<SchoolAiKeyStatus> GetStatusAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        string normalizedProviderId = NormalizeProviderId(providerId);
        cancellationToken.ThrowIfCancellationRequested();

        string? storedValue = await _credentialStore
            .GetSecretAsync(CreateCredentialKey(normalizedProviderId), cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return new SchoolAiKeyStatus(normalizedProviderId, IsConfigured: false, UpdatedUtc: null);
        }

        DateTimeOffset? updatedUtc = TryReadUpdatedUtc(storedValue);
        return new SchoolAiKeyStatus(normalizedProviderId, IsConfigured: true, updatedUtc);
    }

    public Task DeleteKeyAsync(string providerId, CancellationToken cancellationToken = default)
    {
        string normalizedProviderId = NormalizeProviderId(providerId);
        cancellationToken.ThrowIfCancellationRequested();
        return _credentialStore.DeleteSecretAsync(CreateCredentialKey(normalizedProviderId), cancellationToken);
    }

    internal static string CreateCredentialKey(string providerId) =>
        CredentialKeyPrefix + NormalizeProviderId(providerId);

    internal static string NormalizeProviderId(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        string normalized = providerId.Trim().ToLowerInvariant();
        if (normalized.Length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(providerId), providerId, "Provider id must be 64 characters or fewer.");
        }

        foreach (char character in normalized)
        {
            bool isValid = char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '-' or '_';
            if (!isValid)
            {
                throw new ArgumentException(
                    "Provider id may contain only ASCII letters, digits, dots, hyphens, and underscores.",
                    nameof(providerId));
            }
        }

        return normalized;
    }

    private static void ValidateKeyValue(string keyValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyValue);
        if (keyValue.Length > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(keyValue), keyValue.Length, "Provider key is too long.");
        }

        if (keyValue.Any(char.IsWhiteSpace) || keyValue.Any(char.IsControl))
        {
            throw new ArgumentException("Provider key cannot contain whitespace or control characters.", nameof(keyValue));
        }
    }

    private static string CreateStoredValue(DateTimeOffset updatedUtc, string keyValue) =>
        string.Join(
            '\n',
            StoredValuePrefix,
            updatedUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            keyValue);

    private static DateTimeOffset? TryReadUpdatedUtc(string storedValue)
    {
        ReadOnlySpan<char> value = storedValue.AsSpan();
        int prefixEnd = value.IndexOf('\n');
        if (prefixEnd <= 0 ||
            !value[..prefixEnd].SequenceEqual(StoredValuePrefix.AsSpan()))
        {
            return null;
        }

        ReadOnlySpan<char> remainder = value[(prefixEnd + 1)..];
        int timestampEnd = remainder.IndexOf('\n');
        if (timestampEnd <= 0 ||
            !long.TryParse(
                remainder[..timestampEnd],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long unixMilliseconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
