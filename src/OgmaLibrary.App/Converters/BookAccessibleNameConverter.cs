using System.Globalization;
using Avalonia.Data.Converters;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ocr;

namespace OgmaLibrary.App.Converters;

/// <summary>
/// Composes localised, screen-reader friendly text for a catalogue item (Sept-23 Phase 10, K14):
/// "Title, by Author" plus its status badges, the display author with a localised fallback,
/// the item help text and the "cover unavailable" reason.
/// </summary>
/// <remarks>
/// Used as a multi-value converter: value 0 is the <see cref="BookSummaryProjection"/>, value 1 the
/// <see cref="ILocalizationService"/>. The converter parameter selects the output:
/// <c>Name</c> (default), <c>Author</c>, <c>Help</c>, <c>CoverReason</c>, <c>IdentityBadge</c> or
/// <c>IdentityTooltip</c>.
/// </remarks>
public sealed class BookAccessibleNameConverter : IMultiValueConverter
{
    /// <summary>Shared instance for XAML.</summary>
    public static BookAccessibleNameConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 2 ||
            values[0] is not BookSummaryProjection book ||
            values[1] is not ILocalizationService localization)
        {
            return null;
        }

        return (parameter as string) switch
        {
            "Author" => ComposeAuthor(book, localization),
            "Help" => localization["Catalogue.Item.HelpText"],
            "CoverReason" => ComposeCoverReason(book, localization),
            "IdentityBadge" => ComposeIdentityBadge(book, localization),
            "IdentityTooltip" => ComposeIdentityTooltip(book, localization),
            _ => ComposeName(book, localization),
        };
    }

    /// <summary>The first recorded author, or the localised "Unknown author".</summary>
    /// <param name="book">The catalogue item.</param>
    /// <param name="localization">The localisation service.</param>
    /// <returns>The display author.</returns>
    public static string ComposeAuthor(BookSummaryProjection book, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(localization);
        return book.CoverAuthor ?? localization["Catalogue.Item.UnknownAuthor"];
    }

    /// <summary>Composes "Title, by Author" followed by the item's status badges.</summary>
    /// <param name="book">The catalogue item.</param>
    /// <param name="localization">The localisation service.</param>
    /// <returns>The accessible name.</returns>
    public static string ComposeName(BookSummaryProjection book, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(localization);

        string title = book.HasTitle ? book.Title!.Trim() : localization["Search.Result.Untitled"];
        var parts = new List<string>
        {
            string.Format(
                localization.CurrentCulture,
                localization["Catalogue.Item.NameFormat"],
                title,
                ComposeAuthor(book, localization)),
        };

        if (book.IsLocked)
        {
            parts.Add(localization["Catalogue.Badge.Locked"]);
        }

        if (!book.IsAvailable)
        {
            parts.Add(localization["Catalogue.Badge.Unavailable"]);
        }

        if (book.IsFavourite)
        {
            parts.Add(localization["Catalogue.Badge.Favourite"]);
        }

        string? textStatus = TextStatusKey(book.Processing?.TextStatus);
        if (textStatus is not null)
        {
            parts.Add(localization[textStatus]);
        }

        string? identity = ComposeIdentityBadge(book, localization);
        if (identity is not null)
        {
            parts.Add(identity);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// The copies and editions badge (task 10.9): "2 copies", "2 editions · 1 work", both joined by
    /// " · ", or "Possible duplicate" for an ISBN or content-hash candidate; null when none applies.
    /// </summary>
    /// <param name="book">The catalogue item.</param>
    /// <param name="localization">The localisation service.</param>
    /// <returns>The badge text, or <see langword="null"/>.</returns>
    public static string? ComposeIdentityBadge(BookSummaryProjection book, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(localization);
        var parts = new List<string>(2);
        int copies = Math.Max(book.CopyCount, book.OccurrenceCount);
        if (copies > 1)
        {
            parts.Add(string.Format(localization.CurrentCulture, localization["Catalogue.Item.CopiesFormat"], copies));
        }

        if (book.EditionCount > 1)
        {
            parts.Add(string.Format(localization.CurrentCulture, localization["Catalogue.Item.Badge.EditionsFormat"], book.EditionCount));
        }

        if (parts.Count == 0 && book.HasDuplicateCandidate)
        {
            parts.Add(localization["Catalogue.Item.Badge.PossibleDuplicate"]);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>The badge tooltip: the badge text and how to compare, merge or split.</summary>
    /// <param name="book">The catalogue item.</param>
    /// <param name="localization">The localisation service.</param>
    /// <returns>The tooltip, or <see langword="null"/> when the book has no badge.</returns>
    public static string? ComposeIdentityTooltip(BookSummaryProjection book, ILocalizationService localization) =>
        ComposeIdentityBadge(book, localization) is { } badge
            ? string.Format(localization.CurrentCulture, localization["Catalogue.Item.Badge.Tooltip"], badge)
            : null;

    /// <summary>
    /// The localised reason the extracted cover is not shown. When the book has a cover asset but the
    /// image could not be read, the "image unreadable" reason applies.
    /// </summary>
    /// <param name="book">The catalogue item.</param>
    /// <param name="localization">The localisation service.</param>
    /// <returns>The reason text.</returns>
    public static string ComposeCoverReason(BookSummaryProjection book, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(localization);
        return book.CoverUnavailableReason switch
        {
            CoverUnavailableReason.Locked => localization["Catalogue.Cover.Reason.Locked"],
            CoverUnavailableReason.FileMissing => localization["Catalogue.Cover.Reason.FileMissing"],
            CoverUnavailableReason.ExtractionFailed => localization["Catalogue.Cover.Reason.ExtractionFailed"],
            CoverUnavailableReason.NoCoverFound => localization["Catalogue.Cover.Reason.NoCoverFound"],
            _ => localization["Catalogue.Cover.Reason.ImageUnreadable"],
        };
    }

    private static string? TextStatusKey(BookTextStatus? status) => status switch
    {
        BookTextStatus.Searchable => "Catalogue.Badge.TextSearchable",
        BookTextStatus.PartlySearchable => "Catalogue.Badge.TextPartly",
        BookTextStatus.ImageOnly => "Catalogue.Badge.TextImageOnly",
        BookTextStatus.OcrInProgress => "Catalogue.Badge.TextOcrInProgress",
        BookTextStatus.OcrText => "Catalogue.Badge.TextOcr",
        BookTextStatus.NoText => "Catalogue.Badge.TextNone",
        BookTextStatus.OcrFailed => "Catalogue.Badge.TextOcrFailed",
        _ => null,
    };
}
