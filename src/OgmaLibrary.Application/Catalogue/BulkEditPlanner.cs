using System.Globalization;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>A field the bulk-edit preview reports (Sept-23 Phase 10, task 10.8).</summary>
public enum BulkEditField
{
    /// <summary>The book's authors.</summary>
    Author = 0,

    /// <summary>The publication year.</summary>
    Year = 1,

    /// <summary>The rating, 1 to 5.</summary>
    Rating = 2,
}

/// <summary>One before-and-after line of a bulk-edit preview.</summary>
/// <param name="BookId">The book.</param>
/// <param name="Title">The book's display title.</param>
/// <param name="Field">The field.</param>
/// <param name="Before">The current value in invariant form, or <see langword="null"/> when unset.</param>
/// <param name="After">The value after the edit in invariant form, or <see langword="null"/> when unset.</param>
public sealed record BulkEditPreviewRow(
    string BookId,
    string? Title,
    BulkEditField Field,
    string? Before,
    string? After)
{
    /// <summary>Whether applying the edit changes this value.</summary>
    public bool IsChange => !string.Equals(Before, After, StringComparison.Ordinal);
}

/// <summary>The preview of a bulk edit.</summary>
/// <param name="Rows">One row per book and previewed field.</param>
/// <param name="IsValid">Whether the command's values pass <see cref="BulkEditFieldRules"/>.</param>
public sealed record BulkEditPlan(IReadOnlyList<BulkEditPreviewRow> Rows, bool IsValid)
{
    /// <summary>The number of rows whose value changes.</summary>
    public int ChangeCount => Rows.Count(row => row.IsChange);

    /// <summary>Whether the edit is valid and changes at least one value.</summary>
    public bool CanApply => IsValid && ChangeCount > 0;
}

/// <summary>
/// Pure preview of a <see cref="BulkEditCommand"/> against catalogue summaries: author, year and
/// rating before and after. It uses the same parsing as <c>BulkEditAsync</c>, so the preview equals
/// the applied result (Sept-23 Phase 10, task 10.8).
/// </summary>
public static class BulkEditPlanner
{
    /// <summary>Builds the preview rows for the books the command names, in summary order.</summary>
    /// <param name="command">The bulk edit.</param>
    /// <param name="books">Current summaries; books the command does not name are ignored.</param>
    /// <returns>The plan.</returns>
    public static BulkEditPlan Plan(BulkEditCommand command, IReadOnlyList<BookSummaryProjection> books)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(books);

        bool valid = true;
        IReadOnlyList<string> authors = [];
        if (command.AuthorToSet is not null && !BulkEditFieldRules.TryParseAuthors(command.AuthorToSet, out authors))
        {
            valid = false;
        }

        if (command.YearToSet is { } year && !BulkEditFieldRules.IsValidYear(year))
        {
            valid = false;
        }

        if (command.NewRating is { } rating && rating is < 1 or > 5)
        {
            valid = false;
        }

        var selected = new HashSet<string>(command.BookIds, StringComparer.Ordinal);
        var rows = new List<BulkEditPreviewRow>();
        foreach (BookSummaryProjection book in books.Where(book => selected.Remove(book.BookId)))
        {
            if (command.AuthorToSet is not null)
            {
                rows.Add(new BulkEditPreviewRow(
                    book.BookId,
                    book.Title,
                    BulkEditField.Author,
                    BulkEditFieldRules.FormatAuthors(book.Authors),
                    valid ? BulkEditFieldRules.FormatAuthors(authors) : BulkEditFieldRules.FormatAuthors(book.Authors)));
            }

            if (command.YearToSet is not null)
            {
                rows.Add(new BulkEditPreviewRow(
                    book.BookId,
                    book.Title,
                    BulkEditField.Year,
                    Format(book.Year),
                    valid ? Format(command.YearToSet) : Format(book.Year)));
            }

            if (command.NewRating is not null)
            {
                rows.Add(new BulkEditPreviewRow(
                    book.BookId,
                    book.Title,
                    BulkEditField.Rating,
                    Format(book.Rating),
                    valid ? Format(command.NewRating) : Format(book.Rating)));
            }
        }

        return new BulkEditPlan(rows, valid);
    }

    /// <summary>Formats a number the way the preview compares it.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The invariant digits, or <see langword="null"/>.</returns>
    public static string? Format(int? value) => value?.ToString(CultureInfo.InvariantCulture);
}
