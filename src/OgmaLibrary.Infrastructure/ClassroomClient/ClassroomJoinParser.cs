using System.Globalization;
using System.Net;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Parses Host QR/manual join payloads for the Client-mode onboarding flow.</summary>
internal sealed class ClassroomJoinParser : IClassroomJoinParser
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;
    private const int Sha256HexLength = 64;

    public ClassroomJoinRequest Parse(string payload)
    {
        if (TryParse(payload, out ClassroomJoinRequest? request, out string? errorMessage))
        {
            return request!;
        }

        throw new FormatException(errorMessage ?? "The classroom join payload is invalid.");
    }

    public bool TryParse(string payload, out ClassroomJoinRequest? request, out string? errorMessage)
    {
        request = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(payload))
        {
            errorMessage = "Join payload is required.";
            return false;
        }

        if (!Uri.TryCreate(payload.Trim(), UriKind.Absolute, out Uri? uri))
        {
            errorMessage = "Join payload must be an absolute URI.";
            return false;
        }

        Dictionary<string, string> query = ParseQuery(uri.Query);
        return uri.Scheme switch
        {
            "ogma-lan" => TryParseOgmaLan(uri, query, out request, out errorMessage),
            "ogma" => TryParseLegacyOgma(uri, query, out request, out errorMessage),
            _ => Fail("Join payload must use the ogma-lan scheme.", out request, out errorMessage),
        };
    }

    private static bool TryParseOgmaLan(
        Uri uri,
        Dictionary<string, string> query,
        out ClassroomJoinRequest? request,
        out string? errorMessage)
    {
        request = null;
        errorMessage = null;

        if (!uri.AbsolutePath.Equals("/join", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Join payload path must be /join.";
            return false;
        }

        return BuildRequest(
            uri.Host,
            uri.Port,
            query,
            out request,
            out errorMessage);
    }

    private static bool TryParseLegacyOgma(
        Uri uri,
        Dictionary<string, string> query,
        out ClassroomJoinRequest? request,
        out string? errorMessage)
    {
        request = null;
        errorMessage = null;

        if (!query.TryGetValue("addr", out string? addressAndPort))
        {
            errorMessage = "Legacy join payload must include addr.";
            return false;
        }

        if (!TrySplitAddressAndPort(addressAndPort, out string? address, out int port, out errorMessage))
        {
            return false;
        }

        return BuildRequest(address, port, query, out request, out errorMessage);
    }

    private static bool BuildRequest(
        string address,
        int port,
        Dictionary<string, string> query,
        out ClassroomJoinRequest? request,
        out string? errorMessage)
    {
        request = null;
        errorMessage = null;

        if (!IsValidAddress(address))
        {
            errorMessage = "Join payload host address is invalid.";
            return false;
        }

        if (port < MinPort || port > MaxPort)
        {
            errorMessage = "Join payload port is invalid.";
            return false;
        }

        if (!query.TryGetValue("fp", out string? fingerprint) ||
            !TryNormalizeFingerprint(fingerprint, out string normalizedFingerprint))
        {
            errorMessage = "Join payload fingerprint must be a SHA-256 hex value.";
            return false;
        }

        query.TryGetValue("name", out string? displayName);
        query.TryGetValue("code", out string? enrollmentCode);
        query.TryGetValue("auth", out string? authMethod);

        request = new ClassroomJoinRequest(
            address,
            port,
            normalizedFingerprint,
            string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            string.IsNullOrWhiteSpace(enrollmentCode) ? null : enrollmentCode.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(authMethod) ? "enrollment-code" : authMethod.Trim());
        return true;
    }

    private static bool TrySplitAddressAndPort(
        string value,
        out string address,
        out int port,
        out string? errorMessage)
    {
        address = string.Empty;
        port = 0;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Legacy join payload address is required.";
            return false;
        }

        string candidate = value.Trim();
        int separator = candidate.LastIndexOf(':');
        if (separator <= 0 || separator == candidate.Length - 1)
        {
            errorMessage = "Legacy join payload address must include host and port.";
            return false;
        }

        address = candidate[..separator];
        return int.TryParse(candidate[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port);
    }

    private static bool IsValidAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string candidate = address.Trim();
        return IPAddress.TryParse(candidate, out _) ||
            Uri.CheckHostName(candidate) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    private static bool TryNormalizeFingerprint(string fingerprint, out string normalized)
    {
        normalized = string.Concat(fingerprint.Where(static c => c is not (' ' or ':' or '-')))
            .ToLowerInvariant();

        return normalized.Length == Sha256HexLength &&
            normalized.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return values;
        }

        foreach (string pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            string key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            string value = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1].Replace('+', ' '))
                : string.Empty;
            values[key] = value;
        }

        return values;
    }

    private static bool Fail(
        string message,
        out ClassroomJoinRequest? request,
        out string? errorMessage)
    {
        request = null;
        errorMessage = message;
        return false;
    }
}
