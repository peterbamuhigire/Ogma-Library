using System.Text.Json;
using System.Text.Json.Serialization;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>
/// Atomic JSON-backed provider profile settings. Only a platform credential
/// reference may be persisted; API keys never cross this boundary.
/// </summary>
public sealed class AiProviderProfileService : IAiProviderProfileService, IDisposable
{
    private const int MaximumProfiles = 20;
    private const int MaximumTextLength = 256;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the durable profile service at the supplied path.</summary>
    public AiProviderProfileService(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiProviderProfile>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await ReadAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AiProviderProfile> SaveAsync(
        AiProviderProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        AiProviderProfile normalized = Normalize(profile);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AiProviderProfile> profiles = await ReadAsync(cancellationToken).ConfigureAwait(false);
            int existing = profiles.FindIndex(item =>
                string.Equals(item.ProfileId, normalized.ProfileId, StringComparison.Ordinal));
            if (existing < 0 && profiles.Count >= MaximumProfiles)
            {
                throw new InvalidOperationException($"At most {MaximumProfiles} AI provider profiles may be configured.");
            }

            if (normalized.IsDefault)
            {
                for (int index = 0; index < profiles.Count; index++)
                {
                    profiles[index] = profiles[index] with { IsDefault = false };
                }
            }
            if (existing >= 0)
            {
                profiles[existing] = normalized;
            }
            else
            {
                profiles.Add(normalized);
            }

            await WriteAsync(profiles, cancellationToken).ConfigureAwait(false);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        string id = NormalizeProfileId(profileId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AiProviderProfile> profiles = await ReadAsync(cancellationToken).ConfigureAwait(false);
            int removed = profiles.RemoveAll(profile =>
                string.Equals(profile.ProfileId, id, StringComparison.Ordinal));
            if (removed == 0)
            {
                return false;
            }

            await WriteAsync(profiles, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<List<AiProviderProfile>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        using FileStream stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<AiProviderProfile>>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    private async Task WriteAsync(
        IReadOnlyList<AiProviderProfile> profiles,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static AiProviderProfile Normalize(AiProviderProfile profile)
    {
        string profileId = NormalizeProfileId(profile.ProfileId);
        string providerKey = NormalizeText(profile.ProviderKey, nameof(profile.ProviderKey)).ToLowerInvariant();
        string model = NormalizeText(profile.Model, nameof(profile.Model));
        if (providerKey is not ("disabled" or "openai" or "deepseek" or "anthropic" or "ollama"))
        {
            throw new NotSupportedException($"AI provider '{profile.ProviderKey}' is not supported.");
        }
        if (providerKey == "disabled" && (profile.BaseAddress is not null || profile.CredentialReference is not null))
        {
            throw new ArgumentException("The disabled profile cannot contain an endpoint or credential reference.", nameof(profile));
        }
        if (profile.CredentialReference is not null)
        {
            string reference = NormalizeText(profile.CredentialReference, nameof(profile.CredentialReference));
            if (!reference.StartsWith("credential:", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only platform credential references may be persisted.", nameof(profile));
            }
            profile = profile with { CredentialReference = reference };
        }
        if (profile.BaseAddress is not null)
        {
            ValidateEndpoint(providerKey, profile.BaseAddress);
        }

        return profile with
        {
            ProfileId = profileId,
            ProviderKey = providerKey,
            Model = model,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };
    }

    private static void ValidateEndpoint(string providerKey, Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
        {
            throw new ArgumentException("AI profile endpoints must be absolute and contain no credentials or query data.", nameof(endpoint));
        }
        string host = endpoint.Host.TrimEnd('.');
        bool allowed = providerKey switch
        {
            "openai" => string.Equals(host, "api.openai.com", StringComparison.OrdinalIgnoreCase),
            "deepseek" => string.Equals(host, "api.deepseek.com", StringComparison.OrdinalIgnoreCase),
            "anthropic" => string.Equals(host, "api.anthropic.com", StringComparison.OrdinalIgnoreCase),
            "ollama" => endpoint.IsLoopback,
            _ => false,
        };
        if (!allowed)
        {
            throw new ArgumentException($"Endpoint '{endpoint.Host}' is not allowed for provider '{providerKey}'.", nameof(endpoint));
        }
    }

    private static string NormalizeProfileId(string value)
    {
        string id = NormalizeText(value, nameof(value));
        if (id.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ArgumentException("Profile identifiers may contain only letters, digits, '-', '_' and '.'.", nameof(value));
        }
        return id;
    }

    private static string NormalizeText(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > MaximumTextLength)
        {
            throw new ArgumentException($"Profile values are limited to {MaximumTextLength} characters.", parameterName);
        }
        return normalized;
    }
}
