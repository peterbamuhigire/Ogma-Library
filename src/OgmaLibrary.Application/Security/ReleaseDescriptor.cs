using System.Text.Json;

namespace OgmaLibrary.Application.Security;

/// <summary>Describes one immutable Ogma release artifact.</summary>
public sealed record ReleaseDescriptor(
    string Schema,
    string ReleaseId,
    string Version,
    string Platform,
    string RuntimeIdentifier,
    string ArtifactName,
    string ArtifactSha256,
    string SignatureAlgorithm,
    string PublicKeyId)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>The only descriptor schema currently accepted by the updater.</summary>
    public const string CurrentSchema = "ogma-release-v1";

    /// <summary>The detached signature algorithm used for release descriptors.</summary>
    public const string CurrentSignatureAlgorithm = "RSA-PSS-SHA256";

    /// <summary>Parses and validates an exact descriptor JSON document.</summary>
    /// <param name="descriptorJson">The UTF-8 JSON text that was signed.</param>
    /// <param name="descriptor">The validated descriptor when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the descriptor is structurally valid.</returns>
    public static bool TryParse(string descriptorJson, out ReleaseDescriptor? descriptor)
    {
        descriptor = null;
        if (string.IsNullOrWhiteSpace(descriptorJson) || descriptorJson.Length > 16_384)
        {
            return false;
        }

        try
        {
            descriptor = JsonSerializer.Deserialize<ReleaseDescriptor>(
                descriptorJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (descriptor is null ||
            descriptor.Schema != CurrentSchema ||
            descriptor.SignatureAlgorithm != CurrentSignatureAlgorithm ||
            !IsSafeToken(descriptor.ReleaseId, 128) ||
            !IsSafeToken(descriptor.Version, 64) ||
            !IsSupportedPlatform(descriptor.Platform, descriptor.RuntimeIdentifier) ||
            !IsSafeFileName(descriptor.ArtifactName, 255) ||
            !IsSha256(descriptor.ArtifactSha256) ||
            !IsSafeToken(descriptor.PublicKeyId, 128))
        {
            descriptor = null;
            return false;
        }

        return true;
    }

    private static bool IsSupportedPlatform(string platform, string runtimeIdentifier) =>
        (platform, runtimeIdentifier) switch
        {
            ("windows", "win-x64") or ("windows", "win-arm64") => true,
            ("macos", "osx-x64") or ("macos", "osx-arm64") => true,
            _ => false
        };

    private static bool IsSha256(string? value) =>
        value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool IsSafeFileName(string? value, int maxLength) =>
        value is not null &&
        IsSafeToken(value, maxLength) &&
        value.IndexOfAny(['/', '\\', ':']) < 0 &&
        !value.Contains("..", StringComparison.Ordinal);

    private static bool IsSafeToken(string? value, int maxLength) =>
        value is not null &&
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maxLength &&
        value.All(static character => !char.IsControl(character));
}
