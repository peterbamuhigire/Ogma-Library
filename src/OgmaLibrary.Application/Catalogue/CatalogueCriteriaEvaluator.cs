namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Pure in-memory twin of the catalogue read-model criteria (Sept-23 Phase 10, task 10.4).
/// The read model is authoritative; this evaluator applies the same predicates and order to
/// already-projected summaries so the view can re-filter without a database round trip.
/// A parity test binds the two.
/// </summary>
public static class CatalogueCriteriaEvaluator
{
    /// <summary>Filters and orders <paramref name="books"/>; paging is left to the caller.</summary>
    /// <param name="books">The projected summaries.</param>
    /// <param name="filter">The criteria; a null sort field keeps the input order.</param>
    /// <returns>The matching books in criteria order.</returns>
    public static IReadOnlyList<BookSummaryProjection> Apply(
        IEnumerable<BookSummaryProjection> books,
        CatalogueFilter filter)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(filter);

        IEnumerable<BookSummaryProjection> query = books.Where(book => Matches(book, filter));
        return filter.SortField is { } field
            ? Sort(query, field, filter.SortAscending)
            : query.ToList();
    }

    /// <summary>Whether a book satisfies every criterion in <paramref name="filter"/>.</summary>
    /// <param name="book">The projected summary.</param>
    /// <param name="filter">The criteria.</param>
    /// <returns><see langword="true"/> when every set criterion matches.</returns>
    public static bool Matches(BookSummaryProjection book, CatalogueFilter filter)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(filter);

        return (string.IsNullOrWhiteSpace(filter.TitleContains) || TitleMatches(book, filter.TitleContains)) &&
               (string.IsNullOrWhiteSpace(filter.AuthorContains) || AuthorMatches(book, filter.AuthorContains)) &&
               (filter.ShelfId is null || book.ShelfIds.Contains(filter.ShelfId, StringComparer.Ordinal)) &&
               MatchesExtended(book, filter with { TitleContains = null, AuthorContains = null, ShelfId = null });
    }

    /// <summary>
    /// Whether a book satisfies the status and extended criteria only (title, author and
    /// shelf are already applied by the read model's query).
    /// </summary>
    /// <param name="book">The projected summary.</param>
    /// <param name="filter">The criteria.</param>
    /// <returns><see langword="true"/> when every set criterion matches.</returns>
    public static bool MatchesExtended(BookSummaryProjection book, CatalogueFilter filter)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Status is { } status && book.Status != status)
        {
            return false;
        }

        if (filter.MinRating is { } min && (book.Rating is not { } low || low < min))
        {
            return false;
        }

        if (filter.MaxRating is { } max && (book.Rating is not { } high || high > max))
        {
            return false;
        }

        if (filter.IsAvailable is { } available && book.IsAvailable != available)
        {
            return false;
        }

        if (filter.QualityBelow is { } threshold && QualityOf(book) >= threshold)
        {
            return false;
        }

        if (filter.NeedsOcr && !NeedsOcr(book))
        {
            return false;
        }

        if (filter.PossibleDuplicates && !book.IsPossibleDuplicate)
        {
            return false;
        }

        CatalogueMissingFields missing = filter.MissingFields;
        return (!missing.HasFlag(CatalogueMissingFields.Author) || !HasAuthor(book)) &&
               (!missing.HasFlag(CatalogueMissingFields.Isbn) || !book.HasIsbn) &&
               (!missing.HasFlag(CatalogueMissingFields.Cover) || !book.HasExtractedCover) &&
               (!missing.HasFlag(CatalogueMissingFields.Year) || !book.Year.HasValue);
    }

    /// <summary>Case-insensitive match on the display title or the library-relative path.</summary>
    /// <param name="book">The projected summary.</param>
    /// <param name="term">The search term.</param>
    /// <returns>Whether the term occurs.</returns>
    public static bool TitleMatches(BookSummaryProjection book, string term)
    {
        string trimmed = term.Trim();
        return (book.Title?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (book.RelativePath?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Case-insensitive match on any author display name.</summary>
    /// <param name="book">The projected summary.</param>
    /// <param name="term">The search term.</param>
    /// <returns>Whether the term occurs.</returns>
    public static bool AuthorMatches(BookSummaryProjection book, string term)
    {
        string trimmed = term.Trim();
        return book.Authors.Any(author => author.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a non-blank author is recorded.</summary>
    /// <param name="book">The projected summary.</param>
    /// <returns>Whether the book has an author.</returns>
    public static bool HasAuthor(BookSummaryProjection book) =>
        book.Authors.Any(author => !string.IsNullOrWhiteSpace(author));

    /// <summary>Whether OCR would make an unlocked book (more) searchable.</summary>
    /// <param name="book">The projected summary.</param>
    /// <returns>Whether the book needs OCR.</returns>
    public static bool NeedsOcr(BookSummaryProjection book) =>
        book.Processing?.NeedsOcr == true && !book.IsLocked;

    /// <summary>The metadata quality score in [0, 1]; zero before the first enrichment.</summary>
    /// <param name="book">The projected summary.</param>
    /// <returns>The quality score.</returns>
    public static double QualityOf(BookSummaryProjection book) =>
        Math.Clamp(book.Processing?.QualityScore ?? 0, 0, 1);

    /// <summary>
    /// Orders books by <paramref name="field"/>. Books without a value sort last in either
    /// direction; ties fall back to title, then book identity, so the order is total.
    /// </summary>
    /// <param name="books">The books to order.</param>
    /// <param name="field">The sort field.</param>
    /// <param name="ascending">The direction of the primary key.</param>
    /// <returns>The ordered list.</returns>
    public static IReadOnlyList<BookSummaryProjection> Sort(
        IEnumerable<BookSummaryProjection> books,
        CatalogueSortField field,
        bool ascending)
    {
        ArgumentNullException.ThrowIfNull(books);
        var list = books.ToList();
        list.Sort((left, right) => Compare(left, right, field, ascending));
        return list;
    }

    private static int Compare(
        BookSummaryProjection left,
        BookSummaryProjection right,
        CatalogueSortField field,
        bool ascending)
    {
        int primary = field switch
        {
            CatalogueSortField.Title => CompareText(left.Title, right.Title, ascending),
            CatalogueSortField.Author => CompareText(FirstAuthor(left), FirstAuthor(right), ascending),
            CatalogueSortField.Year => CompareNullable(left.Year, right.Year, ascending),
            CatalogueSortField.Rating => CompareNullable(left.Rating, right.Rating, ascending),
            CatalogueSortField.Status => CompareNullable<int>(left.Status, right.Status, ascending),
            CatalogueSortField.DateAdded => CompareNullable(left.DateAddedUtc, right.DateAddedUtc, ascending),
            CatalogueSortField.LastOpened => CompareNullable(left.LastOpenedUtc, right.LastOpenedUtc, ascending),
            CatalogueSortField.FileSize => CompareNullable(left.FileSizeBytes, right.FileSizeBytes, ascending),
            CatalogueSortField.Quality => CompareNullable<double>(QualityOf(left), QualityOf(right), ascending),
            _ => 0,
        };
        if (primary != 0)
        {
            return primary;
        }

        int title = StringComparer.OrdinalIgnoreCase.Compare(left.Title ?? string.Empty, right.Title ?? string.Empty);
        return title != 0 ? title : StringComparer.Ordinal.Compare(left.BookId, right.BookId);
    }

    private static string? FirstAuthor(BookSummaryProjection book) =>
        book.Authors.FirstOrDefault(author => !string.IsNullOrWhiteSpace(author))?.Trim();

    private static int CompareText(string? left, string? right, bool ascending)
    {
        bool leftMissing = string.IsNullOrWhiteSpace(left);
        bool rightMissing = string.IsNullOrWhiteSpace(right);
        if (leftMissing || rightMissing)
        {
            return leftMissing == rightMissing ? 0 : leftMissing ? 1 : -1;
        }

        int result = StringComparer.OrdinalIgnoreCase.Compare(left!.Trim(), right!.Trim());
        return ascending ? result : -result;
    }

    private static int CompareNullable<T>(T? left, T? right, bool ascending)
        where T : struct, IComparable<T>
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue ? 0 : left.HasValue ? -1 : 1;
        }

        int result = left.Value.CompareTo(right.Value);
        return ascending ? result : -result;
    }
}
