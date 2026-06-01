using System.Text.RegularExpressions;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>Builds credential-store keys for protected PDF passwords.</summary>
public static partial class PasswordCredentialKey
{
    /// <summary>The OS credential key prefix for book passwords.</summary>
    public const string Prefix = "Ogma:BookPassword:";

    /// <summary>Creates the credential-store key for a SHA-256 content hash.</summary>
    public static string Create(string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        string normalized = contentHash.Trim().ToLowerInvariant();
        if (!Sha256Regex().IsMatch(normalized))
        {
            throw new ArgumentException("Content hash must be a 64-character hexadecimal SHA-256 hash.", nameof(contentHash));
        }

        return Prefix + normalized;
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}
