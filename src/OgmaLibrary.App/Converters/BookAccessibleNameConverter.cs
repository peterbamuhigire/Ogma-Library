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
/// <c>Name</c> (default), <c>Author</c>, <c>Help</c> or <c>CoverReason</c>.
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

        if (book.CopyCount > 1)
        {
            parts.Add(string.Format(
                localization.CurrentCulture,
                localization["Catalogue.Item.CopiesFormat"],
                book.CopyCount));
        }

        return string.Join(", ", parts);
    }

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
