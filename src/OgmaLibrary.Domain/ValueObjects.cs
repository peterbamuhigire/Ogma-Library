using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OgmaLibrary.Domain;

/// <summary>
/// Compatibility identity of a pre-canonical catalogue presentation.
/// New identity-sensitive code uses the explicit root/occurrence/asset/edition/work IDs.
/// Backed by a ULID-style 26-character Crockford base-32 string so identifiers are
/// sortable by creation time yet collision-resistant.
/// </summary>
/// <param name="Value">The 26-character identifier text.</param>
public readonly record struct BookId(string Value)
{
    /// <summary>Returns the identifier text.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// A validated SHA-256 content hash of a file, used as the strong content identity
/// that lets a renamed or moved file re-match its existing record (FR-LIB-003).
/// </summary>
public readonly record struct ContentHash
{
    /// <summary>The lower-case hexadecimal representation of the 32-byte hash.</summary>
    public string Hex { get; }

    /// <summary>Creates a content hash from a 64-character lower-case hex string.</summary>
    /// <param name="hex">A 64-character hexadecimal SHA-256 digest.</param>
    /// <exception cref="ArgumentException">The value is not a 64-character hex string.</exception>
    public ContentHash(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        if (hex.Length != 64 || !IsHex(hex))
        {
            throw new ArgumentException("A content hash must be a 64-character hexadecimal SHA-256 digest.", nameof(hex));
        }

        Hex = hex.ToLowerInvariant();
    }

    /// <summary>Computes the SHA-256 content hash of the supplied bytes.</summary>
    /// <param name="content">The file content to hash.</param>
    /// <returns>The computed <see cref="ContentHash"/>.</returns>
    public static ContentHash Compute(ReadOnlySpan<byte> content)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(content, digest);
        return new ContentHash(Convert.ToHexStringLower(digest));
    }

    /// <summary>Returns the hexadecimal digest.</summary>
    public override string ToString() => Hex;

    private static bool IsHex(string value)
    {
        foreach (char c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// A validated International Standard Book Number (ISBN-10 or ISBN-13), stored in its
/// normalized digit-only form with the check digit verified (FR-META-001).
/// </summary>
public readonly record struct Isbn
{
    /// <summary>The normalized ISBN (digits only; an ISBN-10 may end with 'X').</summary>
    public string Normalized { get; }

    private Isbn(string normalized) => Normalized = normalized;

    /// <summary>Attempts to parse and validate an ISBN from arbitrary text.</summary>
    /// <param name="candidate">The raw candidate (may contain hyphens or spaces).</param>
    /// <param name="isbn">The parsed ISBN when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the candidate is a valid ISBN-10 or ISBN-13.</returns>
    public static bool TryParse(string? candidate, out Isbn isbn)
    {
        isbn = default;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string digits = new([.. candidate.Where(c => char.IsDigit(c) || c is 'X' or 'x')]);
        digits = digits.ToUpperInvariant();

        bool valid = digits.Length switch
        {
            10 => IsValidIsbn10(digits),
            13 => IsValidIsbn13(digits),
            _ => false,
        };

        if (!valid)
        {
            return false;
        }

        isbn = new Isbn(digits);
        return true;
    }

    /// <summary>Returns the normalized ISBN.</summary>
    public override string ToString() => Normalized;

    private static bool IsValidIsbn10(string s)
    {
        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            if (!char.IsDigit(s[i]))
            {
                return false;
            }

            sum += (s[i] - '0') * (10 - i);
        }

        int check = s[9] == 'X' ? 10 : s[9] - '0';
        sum += check;
        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string s)
    {
        int sum = 0;
        for (int i = 0; i < 13; i++)
        {
            if (!char.IsDigit(s[i]))
            {
                return false;
            }

            sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }
}

/// <summary>
/// A confidence score in the inclusive range [0.0, 1.0], used by the metadata merge
/// policy (FR-META-003) and AI ranking (FR-AI-003).
/// </summary>
public readonly record struct ConfidenceScore
{
    /// <summary>The score in the range [0.0, 1.0].</summary>
    public double Value { get; }

    /// <summary>The human-readable confidence band for this score.</summary>
    public ConfidenceLabel Label =>
        Value switch
        {
            < 0.5 => ConfidenceLabel.Low,
            < 0.75 => ConfidenceLabel.Medium,
            < 0.9 => ConfidenceLabel.High,
            _ => ConfidenceLabel.VeryHigh,
        };

    /// <summary>Creates a confidence score, clamping is rejected in favour of validation.</summary>
    /// <param name="value">A value between 0.0 and 1.0 inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside [0.0, 1.0].</exception>
    public ConfidenceScore(double value)
    {
        if (value is < 0.0 or > 1.0 || double.IsNaN(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A confidence score must be within [0.0, 1.0].");
        }

        Value = value;
    }

    /// <summary>Returns the score formatted with invariant culture.</summary>
    public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);
}

/// <summary>Human-readable confidence band for calibrated recommendation signals.</summary>
public enum ConfidenceLabel
{
    /// <summary>The score is below 0.5.</summary>
    Low = 0,

    /// <summary>The score is at least 0.5 and below 0.75.</summary>
    Medium = 1,

    /// <summary>The score is at least 0.75 and below 0.9.</summary>
    High = 2,

    /// <summary>The score is at least 0.9.</summary>
    VeryHigh = 3,
}
