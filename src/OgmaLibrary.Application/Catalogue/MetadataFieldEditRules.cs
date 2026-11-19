using System.Text.RegularExpressions;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// The catalogue metadata field names a user can edit inline in the book inspector
/// (Sept-23 Phase 11, task 11.4). The names match the provenance rows written by providers.
/// </summary>
public static class EditableMetadataFields
{
    /// <summary>The title, mirrored to the canonical book title.</summary>
    public const string Title = "Title";

    /// <summary>The semicolon-separated author list, mirrored to the linked authors.</summary>
    public const string Author = "Author";

    /// <summary>The publication year, mirrored to the canonical year.</summary>
    public const string Year = "Year";

    /// <summary>The ISBN, mirrored to the canonical normalised ISBN.</summary>
    public const string Isbn = "ISBN";

    /// <summary>The DOI, mirrored to the canonical DOI.</summary>
    public const string Doi = "DOI";

    /// <summary>The publisher, stored only as a provenance-tracked metadata field.</summary>
    public const string Publisher = "Publisher";

    /// <summary>Every editable field, in inspector order.</summary>
    public static IReadOnlyList<string> All { get; } = [Title, Author, Year, Isbn, Doi, Publisher];
}

/// <summary>Why an inline metadata value was rejected.</summary>
public enum MetadataFieldEditError
{
    /// <summary>The value is acceptable.</summary>
    None,

    /// <summary>The text is longer than <see cref="MetadataFieldEditRules.MaxTextLength"/> or holds a control character.</summary>
    Text,

    /// <summary>The author list is not one to twelve names of at most 256 characters.</summary>
    Author,

    /// <summary>The year is not a whole number from 1 to 9999.</summary>
    Year,

    /// <summary>The ISBN is not a valid ISBN-10 or ISBN-13.</summary>
    Isbn,

    /// <summary>The DOI does not have the shape <c>10.NNNN/suffix</c>.</summary>
    Doi,
}

/// <summary>
/// Validation and normalisation for a single inline metadata edit (Sept-23 Phase 11, task 11.4).
/// The inspector uses it for immediate feedback and the write service enforces it again, so a
/// value the inspector shows as accepted is exactly what is stored.
/// </summary>
public static partial class MetadataFieldEditRules
{
    /// <summary>The longest accepted free-text value (title, publisher).</summary>
    public const int MaxTextLength = 512;

    /// <summary>The longest accepted DOI.</summary>
    public const int MaxDoiLength = 256;

    /// <summary>
    /// Validates and normalises <paramref name="raw"/> for <paramref name="fieldName"/>. Blank text
    /// clears the field and is always accepted.
    /// </summary>
    /// <param name="fieldName">The metadata field name.</param>
    /// <param name="raw">The text the user entered.</param>
    /// <param name="normalized">The value to store, or <see langword="null"/> to clear.</param>
    /// <returns><see cref="MetadataFieldEditError.None"/> when the value is acceptable.</returns>
    public static MetadataFieldEditError TryNormalize(string fieldName, string? raw, out string? normalized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return MetadataFieldEditError.None;
        }

        string text = raw.Trim();
        if (Is(fieldName, EditableMetadataFields.Author))
        {
            if (!BulkEditFieldRules.TryParseAuthors(text, out IReadOnlyList<string> authors))
            {
                return MetadataFieldEditError.Author;
            }

            normalized = BulkEditFieldRules.FormatAuthors(authors);
            return MetadataFieldEditError.None;
        }

        if (Is(fieldName, EditableMetadataFields.Year))
        {
            if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int year) ||
                !BulkEditFieldRules.IsValidYear(year))
            {
                return MetadataFieldEditError.Year;
            }

            normalized = year.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return MetadataFieldEditError.None;
        }

        if (Is(fieldName, EditableMetadataFields.Isbn))
        {
            if (text.Any(c => !(char.IsAsciiDigit(c) || c is 'X' or 'x' or '-' or ' ')) ||
                !Isbn.TryParse(text, out Isbn isbn))
            {
                return MetadataFieldEditError.Isbn;
            }

            normalized = isbn.Normalized;
            return MetadataFieldEditError.None;
        }

        if (Is(fieldName, EditableMetadataFields.Doi))
        {
            if (text.Length > MaxDoiLength || !DoiPattern().IsMatch(text))
            {
                return MetadataFieldEditError.Doi;
            }

            normalized = text;
            return MetadataFieldEditError.None;
        }

        if (text.Length > MaxTextLength || text.Any(char.IsControl))
        {
            return MetadataFieldEditError.Text;
        }

        normalized = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return MetadataFieldEditError.None;
    }

    private static bool Is(string fieldName, string expected) =>
        string.Equals(fieldName, expected, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^10\.\d{4,9}/\S+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex DoiPattern();
}
