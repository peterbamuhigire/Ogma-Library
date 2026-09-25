using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OgmaLibrary.Infrastructure.Diagnostics;

/// <summary>
/// The log redaction policy (Sept-23 Phase 02, T02.3). Every string that reaches a log file or
/// a crash marker passes through <see cref="Redact"/>:
/// <list type="bullet">
/// <item>paths inside a library root become <c>&lt;lib&gt;/…/&lt;hash8&gt;.pdf</c>;</item>
/// <item>the user's home directory becomes <c>~</c>;</item>
/// <item>any other PDF file name becomes <c>&lt;hash8&gt;.pdf</c>;</item>
/// <item>provider keys, bearer tokens, passwords and e-mail addresses are replaced.</item>
/// </list>
/// Book titles are never passed to loggers by policy; the PDF-name rule removes the common
/// case where a title leaks through a file name.
/// </summary>
public sealed partial class LogRedactor
{
    /// <summary>Replacement for a library-root prefix.</summary>
    public const string LibraryMarker = "<lib>";

    /// <summary>Replacement for a secret value.</summary>
    public const string SecretMarker = "<secret>";

    /// <summary>Replacement for an e-mail address.</summary>
    public const string EmailMarker = "<email>";

    private static readonly char[] PathTerminators = ['"', '\'', '\r', '\n', '|', '<', '>', '\t'];

    private readonly string? _homeDirectory;
    private readonly Lock _gate = new();
    private string[] _libraryRoots = [];

    /// <summary>Initializes the redactor.</summary>
    /// <param name="homeDirectory">The user's home directory; defaults to the current user profile.</param>
    /// <param name="libraryRoots">Library roots whose contents must never appear in logs.</param>
    public LogRedactor(string? homeDirectory = null, IEnumerable<string>? libraryRoots = null)
    {
        string home = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _homeDirectory = string.IsNullOrWhiteSpace(home) ? null : TrimSeparators(home);
        if (libraryRoots is not null)
        {
            SetLibraryRoots(libraryRoots);
        }
    }

    /// <summary>Replaces the set of library roots (for example after the user chooses a folder).</summary>
    /// <param name="roots">The absolute library root paths.</param>
    public void SetLibraryRoots(IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        string[] normalized = roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(TrimSeparators)
            .Where(root => root.Length > 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(root => root.Length)
            .ToArray();
        lock (_gate)
        {
            _libraryRoots = normalized;
        }
    }

    /// <summary>Returns a short, stable, non-reversible token for <paramref name="value"/>.</summary>
    /// <param name="value">The sensitive value.</param>
    /// <returns>The first eight lowercase hex characters of its SHA-256 digest.</returns>
    public static string Hash8(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        return Convert.ToHexStringLower(digest.AsSpan(0, 4));
    }

    /// <summary>Applies the redaction policy to <paramref name="text"/>.</summary>
    /// <param name="text">Text that may contain sensitive values.</param>
    /// <returns>The redacted text (empty for <see langword="null"/>).</returns>
    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string result = BearerPattern().Replace(text, "Bearer " + SecretMarker);
        result = KeyValueSecretPattern().Replace(result, match =>
            match.Groups["key"].Value + match.Groups["sep"].Value + SecretMarker);
        result = ProviderKeyPattern().Replace(result, SecretMarker);
        result = EmailPattern().Replace(result, EmailMarker);

        string[] roots;
        lock (_gate)
        {
            roots = _libraryRoots;
        }

        foreach (string root in roots)
        {
            result = RedactLibraryRoot(result, root);
        }

        if (_homeDirectory is not null)
        {
            result = ReplaceIgnoreCase(result, _homeDirectory, "~");
        }

        return PdfFileNamePattern().Replace(result, match =>
            Hash8(match.Value) + match.Value[^4..]);
    }

    private static string RedactLibraryRoot(string text, string root)
    {
        var builder = new StringBuilder(text.Length);
        int position = 0;
        while (position < text.Length)
        {
            int index = text.IndexOf(root, position, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                builder.Append(text, position, text.Length - position);
                break;
            }

            builder.Append(text, position, index - position);
            int tailStart = index + root.Length;
            if (tailStart < text.Length && text[tailStart] is not ('\\' or '/'))
            {
                // A longer sibling path (for example "Books2") is not inside this root.
                builder.Append(text, index, root.Length);
                position = tailStart;
                continue;
            }

            int tailEnd = text.IndexOfAny(PathTerminators, tailStart);
            if (tailEnd < 0)
            {
                tailEnd = text.Length;
            }

            string tail = text[tailStart..tailEnd];
            int pdfEnd = tail.IndexOf(".pdf", StringComparison.OrdinalIgnoreCase);
            if (pdfEnd >= 0)
            {
                tail = tail[..(pdfEnd + 4)];
                tailEnd = tailStart + tail.Length;
            }

            builder.Append(LibraryMarker);
            if (tail.Length > 0)
            {
                builder.Append("/…/").Append(Hash8(root + tail));
                if (pdfEnd >= 0)
                {
                    builder.Append(".pdf");
                }
            }

            position = tailEnd;
        }

        return builder.ToString();
    }

    private static string ReplaceIgnoreCase(string text, string value, string replacement)
    {
        int index = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        int position = 0;
        while (index >= 0)
        {
            builder.Append(text, position, index - position).Append(replacement);
            position = index + value.Length;
            index = text.IndexOf(value, position, StringComparison.OrdinalIgnoreCase);
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    private static string TrimSeparators(string path) => path.TrimEnd('\\', '/');

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*")]
    private static partial Regex BearerPattern();

    [GeneratedRegex(
        @"(?i)(?<key>\b(?:api[_-]?key|x-api-key|apikey|key|password|passwd|pwd|secret|client[_-]?secret|access[_-]?token|refresh[_-]?token|token)\b""?)(?<sep>\s*[:=]\s*""?)[^\s""'&,;]+")]
    private static partial Regex KeyValueSecretPattern();

    [GeneratedRegex(@"\b(?:sk-[A-Za-z0-9_\-]{16,}|AIza[0-9A-Za-z_\-]{20,}|gh[pousr]_[A-Za-z0-9]{20,})")]
    private static partial Regex ProviderKeyPattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?i)(?<=[\\/])(?![0-9a-f]{8}\.pdf\b)[^\\/\r\n""'<>|*?]+?\.pdf\b")]
    private static partial Regex PdfFileNamePattern();
}
