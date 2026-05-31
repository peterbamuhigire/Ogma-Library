using OgmaLibrary.Application.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase06;

/// <summary>
/// Unit tests for <see cref="SmartShelfEvaluator"/> (FR-CAT-003).
/// Verifies the closed-enum condition logic for all supported fields and operators.
/// </summary>
public sealed class SmartShelfEvaluatorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> DefaultAuthors = new[] { "Author" };

    private static BookSummaryProjection MakeBook(
        string id = "1",
        int? rating = null,
        int status = 0,
        bool isAvailable = true,
        int? year = null,
        double? progressPct = null) =>
        new(
            BookId: id,
            Title: $"Book {id}",
            Authors: DefaultAuthors,
            CoverRelativePath: null,
            Status: status,
            Rating: rating,
            ShelfIds: Array.Empty<string>(),
            ReadingProgressPct: progressPct,
            IsAvailable: isAvailable,
            Year: year);

    // ── Rating filter tests ───────────────────────────────────────────────────

    /// <summary>
    /// FR-CAT-003: Smart shelf Rating >= 4 returns only books rated 4 or above.
    /// </summary>
    [Fact]
    public void SmartShelf_RatingGte4_ReturnsMatchingBooks()
    {
        var books = new[]
        {
            MakeBook("1", rating: 5),
            MakeBook("2", rating: 4),
            MakeBook("3", rating: 3),
            MakeBook("4", rating: 2),
            MakeBook("5", rating: null),
        };

        var conditions = new[]
        {
            new SmartShelfCondition(SmartShelfField.Rating, SmartShelfOperator.GreaterThanOrEqual, "4"),
        };

        var result = SmartShelfEvaluator.Evaluate(books, conditions);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.BookId == "1");
        Assert.Contains(result, b => b.BookId == "2");
    }

    /// <summary>
    /// Smart shelf Rating == 5 returns only 5-star books.
    /// </summary>
    [Fact]
    public void SmartShelf_RatingEquals5_ReturnsOnlyFiveStarBooks()
    {
        var books = new[]
        {
            MakeBook("1", rating: 5),
            MakeBook("2", rating: 4),
            MakeBook("3", rating: 5),
        };

        var conditions = new[]
        {
            new SmartShelfCondition(SmartShelfField.Rating, SmartShelfOperator.Equals, "5"),
        };

        var result = SmartShelfEvaluator.Evaluate(books, conditions);

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Equal(5, b.Rating));
    }

    /// <summary>
    /// Smart shelf with IsAvailable = true filters to available books only.
    /// </summary>
    [Fact]
    public void SmartShelf_IsAvailableTrue_FiltersToAvailableBooks()
    {
        var books = new[]
        {
            MakeBook("1", isAvailable: true),
            MakeBook("2", isAvailable: false),
            MakeBook("3", isAvailable: true),
        };

        var conditions = new[]
        {
            new SmartShelfCondition(SmartShelfField.IsAvailable, SmartShelfOperator.Equals, "true"),
        };

        var result = SmartShelfEvaluator.Evaluate(books, conditions);

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.True(b.IsAvailable));
    }

    /// <summary>
    /// Smart shelf with no conditions returns all books.
    /// </summary>
    [Fact]
    public void SmartShelf_NoConditions_ReturnsAllBooks()
    {
        var books = Enumerable.Range(1, 5)
            .Select(i => MakeBook(i.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();

        var result = SmartShelfEvaluator.Evaluate(books, Array.Empty<SmartShelfCondition>());

        Assert.Equal(5, result.Count);
    }

    /// <summary>
    /// Conjunction: Rating >= 4 AND Status == 0 returns only books meeting both conditions.
    /// </summary>
    [Fact]
    public void SmartShelf_ConjunctiveConditions_ReturnsBooksMatchingAll()
    {
        var books = new[]
        {
            MakeBook("1", rating: 5, status: 0),
            MakeBook("2", rating: 5, status: 1),
            MakeBook("3", rating: 3, status: 0),
            MakeBook("4", rating: 4, status: 0),
        };

        var conditions = new[]
        {
            new SmartShelfCondition(SmartShelfField.Rating, SmartShelfOperator.GreaterThanOrEqual, "4"),
            new SmartShelfCondition(SmartShelfField.Status, SmartShelfOperator.Equals, "0"),
        };

        var result = SmartShelfEvaluator.Evaluate(books, conditions);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.BookId == "1");
        Assert.Contains(result, b => b.BookId == "4");
    }

    /// <summary>
    /// Year less than 2000 returns only books published before 2000.
    /// </summary>
    [Fact]
    public void SmartShelf_YearLessThan2000_ReturnsOldBooks()
    {
        var books = new[]
        {
            MakeBook("1", year: 1995),
            MakeBook("2", year: 2010),
            MakeBook("3", year: 1999),
            MakeBook("4", year: null),
        };

        var conditions = new[]
        {
            new SmartShelfCondition(SmartShelfField.Year, SmartShelfOperator.LessThan, "2000"),
        };

        var result = SmartShelfEvaluator.Evaluate(books, conditions);

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.True(b.Year < 2000));
    }
}
