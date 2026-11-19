namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Validation and normalisation shared by single-field edits, bulk edits and the bulk-edit
/// preview, so the preview shows exactly what is written (Sept-23 Phase 10, task 10.8).
/// </summary>
public static class BulkEditFieldRules
{
    /// <summary>The longest accepted author name.</summary>
    public const int MaxAuthorLength = 256;

    /// <summary>The most authors one edit may set.</summary>
    public const int MaxAuthors = 12;

    /// <summary>The earliest accepted year.</summary>
    public const int MinYear = 1;

    /// <summary>The latest accepted year.</summary>
    public const int MaxYear = 9999;

    /// <summary>
    /// Parses a semicolon-separated author list: trims, collapses inner whitespace, drops empty
    /// parts and case-insensitive repeats. Fails when nothing remains, a name is too long or holds
    /// a control character, or there are more than <see cref="MaxAuthors"/> names.
    /// </summary>
    /// <param name="value">The raw author text.</param>
    /// <param name="authors">The normalised names in entry order.</param>
    /// <returns><see langword="true"/> when the value is acceptable.</returns>
    public static bool TryParseAuthors(string? value, out IReadOnlyList<string> authors)
    {
        authors = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var names = new List<string>();
        foreach (string part in value.Split(';'))
        {
            if (part.Any(char.IsControl))
            {
                return false;
            }

            string name = string.Join(' ', part.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (name.Length == 0)
            {
                continue;
            }

            if (name.Length > MaxAuthorLength)
            {
                return false;
            }

            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        if (names.Count == 0 || names.Count > MaxAuthors)
        {
            return false;
        }

        authors = names;
        return true;
    }

    /// <summary>Whether <paramref name="year"/> is an accepted publication year.</summary>
    /// <param name="year">The year.</param>
    /// <returns><see langword="true"/> when within <see cref="MinYear"/> to <see cref="MaxYear"/>.</returns>
    public static bool IsValidYear(int year) => year is >= MinYear and <= MaxYear;

    /// <summary>
    /// Throws when a single-field edit of Author or Year carries a value the bulk path would
    /// reject. A <see langword="null"/> value clears the field and is always accepted.
    /// </summary>
    /// <param name="fieldName">The metadata field name.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentException">The value is not acceptable.</exception>
    public static void ValidateMetadataField(string fieldName, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.Equals(fieldName, "Author", StringComparison.OrdinalIgnoreCase) &&
            !TryParseAuthors(value, out _))
        {
            throw new ArgumentException("Author must hold one to twelve names of at most 256 characters each.", nameof(value));
        }

        if (string.Equals(fieldName, "Year", StringComparison.OrdinalIgnoreCase) &&
            (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int year) ||
             !IsValidYear(year)))
        {
            throw new ArgumentException("Year must be a whole number from 1 to 9999.", nameof(value));
        }
    }

    /// <summary>Formats an author list the way the preview and the applied result are compared.</summary>
    /// <param name="authors">The names.</param>
    /// <returns>The names joined by "; ", or <see langword="null"/> when there are none.</returns>
    public static string? FormatAuthors(IReadOnlyList<string> authors) =>
        authors.Count == 0 ? null : string.Join("; ", authors);
}
