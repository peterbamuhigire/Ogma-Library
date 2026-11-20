using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Pure formatters that turn a <see cref="CitationCard"/> into BibTeX, RIS, CSL-JSON, Markdown or
/// plain text (READ-013). Output is deterministic: line endings are always <c>\n</c> and there is
/// no timestamp, so golden files are stable. Missing title or author are omitted from the
/// machine formats rather than invented.
/// </summary>
public static class CitationFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Formats a card.</summary>
    /// <param name="card">The card.</param>
    /// <param name="format">The target format.</param>
    /// <param name="unknownAuthor">Fallback author for the human-readable formats.</param>
    /// <param name="unknownTitle">Fallback title for the human-readable formats.</param>
    /// <param name="pageFormat">Composite format for the page, for example <c>p.{0}</c>.</param>
    /// <returns>The formatted text.</returns>
    public static string Format(
        CitationCard card,
        CitationFormat format,
        string unknownAuthor = "Unknown author",
        string unknownTitle = "Unknown title",
        string pageFormat = "p.{0}")
    {
        ArgumentNullException.ThrowIfNull(card);
        return format switch
        {
            CitationFormat.BibTeX => ToBibTeX(card),
            CitationFormat.Ris => ToRis(card),
            CitationFormat.CslJson => ToCslJson(card),
            CitationFormat.Markdown => ToMarkdown(card, unknownAuthor, unknownTitle, pageFormat),
            _ => card.ToPlainText(unknownAuthor, unknownTitle, pageFormat),
        };
    }

    private static string ToBibTeX(CitationCard card)
    {
        var builder = new StringBuilder();
        builder.Append("@misc{").Append(BibKey(card)).Append(",\n");
        if (!string.IsNullOrWhiteSpace(card.Author))
        {
            builder.Append("  author = {").Append(BibEscape(card.Author)).Append("},\n");
        }

        if (!string.IsNullOrWhiteSpace(card.Title))
        {
            builder.Append("  title = {").Append(BibEscape(card.Title)).Append("},\n");
        }

        builder.Append("  pages = {").Append(card.PageNumber.ToString(CultureInfo.InvariantCulture)).Append("},\n");
        builder.Append("  note = {").Append(BibEscape(Flatten(card.SelectedText))).Append("}\n");
        builder.Append("}\n");
        return builder.ToString();
    }

    private static string ToRis(CitationCard card)
    {
        var builder = new StringBuilder();
        builder.Append("TY  - GEN\n");
        if (!string.IsNullOrWhiteSpace(card.Author))
        {
            builder.Append("AU  - ").Append(Flatten(card.Author)).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(card.Title))
        {
            builder.Append("TI  - ").Append(Flatten(card.Title)).Append('\n');
        }

        builder.Append("SP  - ").Append(card.PageNumber.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("N1  - ").Append(Flatten(card.SelectedText)).Append('\n');
        builder.Append("ER  - \n");
        return builder.ToString();
    }

    private static string ToCslJson(CitationCard card)
    {
        var item = new Dictionary<string, object>
        {
            ["id"] = string.Create(CultureInfo.InvariantCulture, $"{card.BookId}-p{card.PageNumber}"),
            ["type"] = "book",
        };
        if (!string.IsNullOrWhiteSpace(card.Title))
        {
            item["title"] = card.Title;
        }

        if (!string.IsNullOrWhiteSpace(card.Author))
        {
            item["author"] = new[] { new Dictionary<string, string> { ["literal"] = card.Author } };
        }

        item["page"] = card.PageNumber.ToString(CultureInfo.InvariantCulture);
        item["note"] = card.SelectedText;
        string json = JsonSerializer.Serialize(new[] { item }, JsonOptions);
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static string ToMarkdown(CitationCard card, string unknownAuthor, string unknownTitle, string pageFormat)
    {
        string author = MarkdownEscape(card.Author ?? unknownAuthor);
        string title = MarkdownEscape(card.Title ?? unknownTitle);
        string page = string.Format(CultureInfo.CurrentCulture, pageFormat, card.PageNumber);
        var builder = new StringBuilder();
        foreach (string line in card.SelectedText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.Append("> ").Append(line).Append('\n');
        }

        builder.Append('\n');
        builder.Append("— ").Append(author).Append(", *").Append(title).Append("*, ").Append(page).Append('\n');
        return builder.ToString();
    }

    private static string BibKey(CitationCard card)
    {
        string lastName = Alnum(card.Author?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault());
        string firstWord = Alnum(card.Title?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
        string stem = (lastName + firstWord).Length > 0 ? lastName + firstWord : "citation";
        return string.Create(CultureInfo.InvariantCulture, $"{stem}p{card.PageNumber}");
    }

    private static string Alnum(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char c in value.Normalize(NormalizationForm.FormD))
        {
            if (c < 128 && char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static string Flatten(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string BibEscape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            if (c is '&' or '%' or '$' or '#' or '_' or '{' or '}')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string MarkdownEscape(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        foreach (char c in value)
        {
            if (c is '*' or '_' or '`' or '[' or ']' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
