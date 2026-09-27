using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// One visible catalogue book as the unified search sees it: display fields resolved the
/// same way as the catalogue projection (title, then the best metadata title, then the file
/// name) and the fields metadata search matches on (Sept-23 Phase 13).
/// </summary>
internal sealed record SearchBookCandidate(
    string BookId,
    string DisplayTitle,
    string? DisplayAuthor,
    IReadOnlyList<string> Titles,
    IReadOnlyList<string> Authors,
    string? IsbnDigits,
    int? Year,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Shelves,
    string? FileName,
    int TextStatus,
    int IndexStatus,
    bool IsLocked);

/// <summary>Loads <see cref="SearchBookCandidate"/> rows for the books the catalogue shows.</summary>
internal static class SearchBookCandidateLoader
{
    private static readonly string[] LoadedFields =
        ["Title", "Author", "Tag", "Tags", "Subject", "Subjects", "Categories", "Category", "Year", "PublishedDate", "Isbn", "ISBN"];

    /// <summary>
    /// Loads every book the catalogue lists (Sept-23 Phase 05 visibility: at least one
    /// valid or locked file in an enabled library folder, or a loose file).
    /// </summary>
    public static async Task<List<SearchBookCandidate>> LoadAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        HashSet<string> activeRootIds = await LibraryRootPaths
            .GetActiveRootIdsAsync(context, cancellationToken)
            .ConfigureAwait(false);
        List<string> activeRoots = [.. activeRootIds];
        var rows = await context.Books
            .AsNoTracking()
            .Where(book => book.Status == 0)
            .Where(book =>
                !book.BookFiles.Any() ||
                book.BookFiles.Any(file =>
                    (file.FileValidity == 0 || file.FileValidity == 4) &&
                    (file.LibraryRootId == null || activeRoots.Contains(file.LibraryRootId))))
            .Select(book => new
            {
                book.BookId,
                book.Title,
                book.RelativePath,
                book.IsbnNormalized,
                book.Year,
                book.TextStatus,
                book.IndexStatus,
                IsLocked = book.IsPasswordProtected || book.BookFiles.Any(file => file.FileValidity == 4),
                PrimaryPath = book.BookFiles
                    .OrderBy(file => file.FileStatus)
                    .ThenBy(file => file.BookFileId)
                    .Select(file => file.RelativePath)
                    .FirstOrDefault(),
                Authors = book.BookAuthors
                    .OrderBy(link => link.DisplayOrder)
                    .Select(link => link.Author!.NormalizedName)
                    .ToList(),
                Shelves = book.ShelfBooks
                    .Select(link => link.Shelf!.Name)
                    .ToList(),
                Fields = book.MetadataFields
                    .Where(field => LoadedFields.Contains(field.FieldName) && field.Value != null)
                    .Select(field => new MetadataValue(field.FieldName, field.Value!, field.Source, field.Confidence, field.IsOverridden))
                    .ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row =>
            {
                string? metadataTitle = BestValue(row.Fields, "Title");
                string? fileName = FileName(row.PrimaryPath ?? row.RelativePath);
                string displayTitle = !string.IsNullOrWhiteSpace(row.Title)
                    ? row.Title.Trim()
                    : !string.IsNullOrWhiteSpace(metadataTitle)
                        ? metadataTitle.Trim()
                        : fileName ?? row.BookId;
                List<string> authors = row.Authors.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
                if (authors.Count == 0 && BestValue(row.Fields, "Author") is { } metadataAuthor)
                {
                    authors = metadataAuthor
                        .Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();
                }

                List<string> titles = [displayTitle];
                titles.AddRange(Values(row.Fields, "Title").Where(title =>
                    !string.Equals(title, displayTitle, StringComparison.Ordinal)));
                string? isbn = Digits(row.IsbnNormalized) ?? Digits(BestValue(row.Fields, "Isbn") ?? BestValue(row.Fields, "ISBN"));
                return new SearchBookCandidate(
                    row.BookId,
                    displayTitle,
                    authors.FirstOrDefault(),
                    titles,
                    authors,
                    isbn,
                    row.Year ?? ParseYear(BestValue(row.Fields, "Year") ?? BestValue(row.Fields, "PublishedDate")),
                    Values(row.Fields, "Tag", "Tags", "Subject", "Subjects", "Categories", "Category")
                        .SelectMany(value => value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .ToList(),
                    row.Shelves.Where(name => !string.IsNullOrWhiteSpace(name)).ToList(),
                    fileName,
                    row.TextStatus,
                    row.IndexStatus,
                    row.IsLocked);
            })
            .ToList();
    }

    /// <summary>Computes the searchable-text coverage for the given books.</summary>
    public static SearchIndexCoverage Coverage(IEnumerable<SearchBookCandidate> books)
    {
        int total = 0;
        int searchable = 0;
        int needOcr = 0;
        int pending = 0;
        foreach (SearchBookCandidate book in books)
        {
            total++;
            switch ((BookTextStatus)book.TextStatus)
            {
                case BookTextStatus.Searchable:
                case BookTextStatus.PartlySearchable:
                case BookTextStatus.OcrText:
                    searchable++;
                    break;
                case BookTextStatus.ImageOnly:
                case BookTextStatus.OcrFailed:
                    needOcr++;
                    break;
                case BookTextStatus.OcrInProgress:
                    pending++;
                    break;
                case BookTextStatus.Unknown when book.IndexStatus == (int)SearchBookIndexStatus.Indexed:
                    searchable++;
                    break;
                case BookTextStatus.Unknown when !book.IsLocked:
                    pending++;
                    break;
                default:
                    break;
            }
        }

        return new SearchIndexCoverage(total, searchable, needOcr, pending);
    }

    private static string? BestValue(IEnumerable<MetadataValue> fields, string name) =>
        fields
            .Where(field => string.Equals(field.FieldName, name, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(field.Value))
            .OrderByDescending(field => field.IsOverridden)
            .ThenByDescending(field => field.Confidence ?? 0)
            .ThenBy(field => field.Source, StringComparer.Ordinal)
            .Select(field => field.Value)
            .FirstOrDefault();

    private static IEnumerable<string> Values(IEnumerable<MetadataValue> fields, params string[] names) =>
        fields
            .Where(field => names.Contains(field.FieldName, StringComparer.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => field.Value.Trim())
            .Distinct(StringComparer.Ordinal);

    private static string? FileName(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string name = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        string withoutExtension = Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrWhiteSpace(withoutExtension) ? null : withoutExtension;
    }

    private static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string digits = new(value.Where(ch => char.IsDigit(ch) || ch is 'X' or 'x').Select(char.ToUpperInvariant).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 4)
        {
            return null;
        }

        return int.TryParse(value.AsSpan(0, 4), out int year) ? year : null;
    }

    private sealed record MetadataValue(string FieldName, string Value, string? Source, double? Confidence, bool IsOverridden);
}
