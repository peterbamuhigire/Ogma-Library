using System.Text.Json;
using System.Text.Json.Serialization;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// The catalogue fields that a smart-shelf condition can evaluate (FR-CAT-003).
/// This is a CLOSED enum — no dynamic code generation or injection is possible.
/// </summary>
public enum SmartShelfField
{
    /// <summary>The reader's rating (1–5).</summary>
    Rating,

    /// <summary>The lifecycle status (0=Active, 1=Unavailable, 2=Excluded).</summary>
    Status,

    /// <summary>The publication year.</summary>
    Year,

    /// <summary>Whether the physical file is present on disk.</summary>
    IsAvailable,

    /// <summary>The reading-progress completion percentage [0.0, 100.0].</summary>
    ReadingProgressPct,

    /// <summary>The display title or library path (text; <see cref="SmartShelfOperator.Contains"/> only).</summary>
    Title,

    /// <summary>Any author display name (text; <see cref="SmartShelfOperator.Contains"/> only).</summary>
    Author,

    /// <summary>The metadata quality score in [0, 1].</summary>
    QualityScore,

    /// <summary>Whether an author is recorded (boolean).</summary>
    HasAuthor,

    /// <summary>Whether an ISBN is recorded (boolean).</summary>
    HasIsbn,

    /// <summary>Whether an extracted cover exists (boolean).</summary>
    HasCover,

    /// <summary>Whether a publication year is recorded (boolean).</summary>
    HasYear,

    /// <summary>Whether OCR would make the book (more) searchable (boolean).</summary>
    NeedsOcr,

    /// <summary>Whether the book has several copies or editions (boolean).</summary>
    PossibleDuplicate,
}

/// <summary>
/// The comparison operators supported by a smart-shelf condition (FR-CAT-003).
/// This is a CLOSED enum — no dynamic code generation or injection is possible.
/// </summary>
public enum SmartShelfOperator
{
    /// <summary>Numeric equality.</summary>
    Equals,

    /// <summary>Numeric not-equal.</summary>
    NotEquals,

    /// <summary>Greater-than.</summary>
    GreaterThan,

    /// <summary>Greater-than-or-equal.</summary>
    GreaterThanOrEqual,

    /// <summary>Less-than.</summary>
    LessThan,

    /// <summary>Less-than-or-equal.</summary>
    LessThanOrEqual,

    /// <summary>Case-insensitive substring match; text fields only.</summary>
    Contains,
}

/// <summary>
/// A single condition in a smart-shelf filter (FR-CAT-003).
/// Stored as JSON in <c>Shelves.Query</c>; evaluated by
/// <see cref="SmartShelfEvaluator"/> against the in-memory
/// <see cref="BookSummaryProjection"/> list.
/// </summary>
/// <param name="Field">The catalogue field to compare.</param>
/// <param name="Operator">The comparison operator.</param>
/// <param name="Value">The comparison value (serialized as a double for numeric fields; "true"/"false" for bool).</param>
public sealed record SmartShelfCondition(
    SmartShelfField Field,
    SmartShelfOperator Operator,
    string Value);

/// <summary>
/// Parses the persisted smart-shelf query contract without accepting executable
/// expressions or open-ended field names. The canonical JSON shape is an array
/// of <see cref="SmartShelfCondition"/> objects.
/// </summary>
public static class SmartShelfQueryParser
{
    private const int MaxConditions = 32;
    private const int MaxValueLength = 128;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Serialises conditions to the canonical JSON contract (enum names, not numbers), so
    /// <see cref="TryParse"/> reads back exactly the same conditions.
    /// </summary>
    /// <param name="conditions">The conditions to save.</param>
    /// <returns>The JSON array.</returns>
    public static string Serialize(IReadOnlyList<SmartShelfCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        return JsonSerializer.Serialize(conditions, Options);
    }

    /// <summary>Attempts to parse and validate a saved smart-shelf query.</summary>
    public static bool TryParse(
        string? json,
        out IReadOnlyList<SmartShelfCondition> conditions)
    {
        conditions = [];
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4096)
        {
            return false;
        }

        try
        {
            List<SmartShelfCondition>? parsed = JsonSerializer.Deserialize<List<SmartShelfCondition>>(
                json,
                Options);
            if (parsed is null || parsed.Count > MaxConditions || parsed.Any(condition =>
                    !Enum.IsDefined(condition.Field) ||
                    !Enum.IsDefined(condition.Operator) ||
                    string.IsNullOrWhiteSpace(condition.Value) ||
                    condition.Value.Length > MaxValueLength ||
                    !IsSemanticallyValid(condition)))
            {
                return false;
            }

            conditions = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="field"/> holds text and accepts only <see cref="SmartShelfOperator.Contains"/>.</summary>
    internal static bool IsTextField(SmartShelfField field) =>
        field is SmartShelfField.Title or SmartShelfField.Author;

    /// <summary>Whether <paramref name="field"/> holds a boolean value.</summary>
    internal static bool IsBooleanField(SmartShelfField field) =>
        field is SmartShelfField.IsAvailable or SmartShelfField.HasAuthor or SmartShelfField.HasIsbn or
            SmartShelfField.HasCover or SmartShelfField.HasYear or SmartShelfField.NeedsOcr or
            SmartShelfField.PossibleDuplicate;

    private static bool IsSemanticallyValid(SmartShelfCondition condition)
    {
        if (IsTextField(condition.Field))
        {
            return condition.Operator == SmartShelfOperator.Contains;
        }

        if (condition.Operator == SmartShelfOperator.Contains)
        {
            return false;
        }

        if (IsBooleanField(condition.Field))
        {
            return condition.Operator is (SmartShelfOperator.Equals or SmartShelfOperator.NotEquals) &&
                   bool.TryParse(condition.Value, out _);
        }

        return double.TryParse(
                   condition.Value,
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out double value) &&
               double.IsFinite(value);
    }
}

/// <summary>
/// Evaluates a list of <see cref="SmartShelfCondition"/> records against an
/// in-memory catalogue projection (FR-CAT-003, MVP). All conditions are
/// combined conjunctively (AND). Smart shelves with no conditions return all books.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator is intentionally CLOSED: only <see cref="SmartShelfField"/>
/// and <see cref="SmartShelfOperator"/> enum values are supported. No dynamic
/// LINQ expression trees or code-gen paths exist, which prevents injection
/// through crafted <c>Query</c> JSON (Risk R2 from the phase README).
/// </para>
/// <para>
/// Phase 10 will extend smart shelves to full-text conditions; the extension
/// point is a separate <c>IFullTextShelfEvaluator</c> — not a change to this type.
/// </para>
/// </remarks>
public static class SmartShelfEvaluator
{
    /// <summary>
    /// Filters <paramref name="books"/> to those that satisfy every condition in
    /// <paramref name="conditions"/> (conjunctive AND).
    /// </summary>
    /// <param name="books">The source projection list.</param>
    /// <param name="conditions">Zero or more conditions; an empty list matches all books.</param>
    /// <returns>The filtered subset.</returns>
    public static IReadOnlyList<BookSummaryProjection> Evaluate(
        IEnumerable<BookSummaryProjection> books,
        IEnumerable<SmartShelfCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(conditions);

        var conditionList = conditions as IReadOnlyList<SmartShelfCondition>
            ?? conditions.ToList();

        if (conditionList.Count == 0)
        {
            return books as IReadOnlyList<BookSummaryProjection> ?? books.ToList();
        }

        return books.Where(b => conditionList.All(c => Matches(b, c))).ToList();
    }

    private static bool Matches(BookSummaryProjection book, SmartShelfCondition condition)
    {
        if (SmartShelfQueryParser.IsTextField(condition.Field))
        {
            return condition.Operator == SmartShelfOperator.Contains &&
                   (condition.Field == SmartShelfField.Title
                       ? CatalogueCriteriaEvaluator.TitleMatches(book, condition.Value)
                       : CatalogueCriteriaEvaluator.AuthorMatches(book, condition.Value));
        }

        if (SmartShelfQueryParser.IsBooleanField(condition.Field))
        {
            // Boolean values are "true"/"false"; anything else fails closed.
            if (!bool.TryParse(condition.Value, out bool boolCondition))
            {
                return false;
            }

            bool boolField = GetBooleanValue(book, condition.Field);
            return condition.Operator switch
            {
                SmartShelfOperator.Equals => boolField == boolCondition,
                SmartShelfOperator.NotEquals => boolField != boolCondition,
                _ => false,
            };
        }

        double? fieldValue = GetFieldValue(book, condition.Field);
        if (!double.TryParse(condition.Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double conditionValue))
        {
            return false;
        }

        if (fieldValue is null)
        {
            // Null fields never satisfy numeric comparisons
            return false;
        }

        return condition.Operator switch
        {
            SmartShelfOperator.Equals => Math.Abs(fieldValue.Value - conditionValue) < 1e-9,
            SmartShelfOperator.NotEquals => Math.Abs(fieldValue.Value - conditionValue) >= 1e-9,
            SmartShelfOperator.GreaterThan => fieldValue.Value > conditionValue,
            SmartShelfOperator.GreaterThanOrEqual => fieldValue.Value >= conditionValue,
            SmartShelfOperator.LessThan => fieldValue.Value < conditionValue,
            SmartShelfOperator.LessThanOrEqual => fieldValue.Value <= conditionValue,
            _ => false,
        };
    }

    private static double? GetFieldValue(BookSummaryProjection book, SmartShelfField field) =>
        field switch
        {
            SmartShelfField.Rating => book.Rating.HasValue ? (double)book.Rating.Value : null,
            SmartShelfField.Status => (double)book.Status,
            SmartShelfField.Year => book.Year.HasValue ? (double)book.Year.Value : null,
            SmartShelfField.ReadingProgressPct => book.ReadingProgressPct,
            SmartShelfField.QualityScore => CatalogueCriteriaEvaluator.QualityOf(book),
            _ => null,
        };

    private static bool GetBooleanValue(BookSummaryProjection book, SmartShelfField field) =>
        field switch
        {
            SmartShelfField.IsAvailable => book.IsAvailable,
            SmartShelfField.HasAuthor => CatalogueCriteriaEvaluator.HasAuthor(book),
            SmartShelfField.HasIsbn => book.HasIsbn,
            SmartShelfField.HasCover => book.HasExtractedCover,
            SmartShelfField.HasYear => book.Year.HasValue,
            SmartShelfField.NeedsOcr => CatalogueCriteriaEvaluator.NeedsOcr(book),
            SmartShelfField.PossibleDuplicate => book.IsPossibleDuplicate,
            _ => false,
        };
}

/// <summary>
/// Converts between the catalogue criteria model and saved smart-shelf conditions
/// (Sept-23 Phase 10), so "Save as smart shelf" stores exactly the live filter and a
/// saved shelf can be reopened as the same filter. Paging and sort are not criteria.
/// </summary>
public static class SmartShelfCriteria
{
    private static readonly System.Globalization.CultureInfo Invariant =
        System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Builds the conjunctive conditions equivalent to <paramref name="filter"/>.</summary>
    /// <param name="filter">The live criteria; its shelf, paging and sort are ignored.</param>
    /// <returns>Zero or more conditions.</returns>
    public static IReadOnlyList<SmartShelfCondition> ToConditions(CatalogueFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var conditions = new List<SmartShelfCondition>();
        if (!string.IsNullOrWhiteSpace(filter.TitleContains))
        {
            conditions.Add(new(SmartShelfField.Title, SmartShelfOperator.Contains, filter.TitleContains.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(filter.AuthorContains))
        {
            conditions.Add(new(SmartShelfField.Author, SmartShelfOperator.Contains, filter.AuthorContains.Trim()));
        }

        if (filter.Status is { } status)
        {
            conditions.Add(new(SmartShelfField.Status, SmartShelfOperator.Equals, status.ToString(Invariant)));
        }

        if (filter.MinRating is { } min)
        {
            conditions.Add(new(SmartShelfField.Rating, SmartShelfOperator.GreaterThanOrEqual, min.ToString(Invariant)));
        }

        if (filter.MaxRating is { } max)
        {
            conditions.Add(new(SmartShelfField.Rating, SmartShelfOperator.LessThanOrEqual, max.ToString(Invariant)));
        }

        if (filter.IsAvailable is { } available)
        {
            conditions.Add(Flag(SmartShelfField.IsAvailable, available));
        }

        if (filter.QualityBelow is { } quality)
        {
            conditions.Add(new(SmartShelfField.QualityScore, SmartShelfOperator.LessThan, quality.ToString("R", Invariant)));
        }

        AddMissing(conditions, filter.MissingFields, CatalogueMissingFields.Author, SmartShelfField.HasAuthor);
        AddMissing(conditions, filter.MissingFields, CatalogueMissingFields.Isbn, SmartShelfField.HasIsbn);
        AddMissing(conditions, filter.MissingFields, CatalogueMissingFields.Cover, SmartShelfField.HasCover);
        AddMissing(conditions, filter.MissingFields, CatalogueMissingFields.Year, SmartShelfField.HasYear);

        if (filter.NeedsOcr)
        {
            conditions.Add(Flag(SmartShelfField.NeedsOcr, true));
        }

        if (filter.PossibleDuplicates)
        {
            conditions.Add(Flag(SmartShelfField.PossibleDuplicate, true));
        }

        return conditions;
    }

    /// <summary>
    /// Rebuilds the criteria from saved conditions. Conditions the criteria model cannot
    /// express exactly (for example "year after 2000") return <see langword="false"/>.
    /// </summary>
    /// <param name="conditions">Validated conditions.</param>
    /// <param name="filter">The equivalent criteria when the conversion is exact.</param>
    /// <returns>Whether every condition maps to a criterion.</returns>
    public static bool TryToFilter(IReadOnlyList<SmartShelfCondition> conditions, out CatalogueFilter filter)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        filter = new CatalogueFilter();
        foreach (SmartShelfCondition condition in conditions)
        {
            CatalogueFilter? next = Apply(filter, condition);
            if (next is null)
            {
                filter = new CatalogueFilter();
                return false;
            }

            filter = next;
        }

        return true;
    }

    private static CatalogueFilter? Apply(CatalogueFilter filter, SmartShelfCondition condition)
    {
        bool? flag = bool.TryParse(condition.Value, out bool parsedFlag) ? parsedFlag : null;
        bool isEquals = condition.Operator == SmartShelfOperator.Equals;
        int? integer = int.TryParse(condition.Value, System.Globalization.NumberStyles.Integer, Invariant, out int parsedInt)
            ? parsedInt
            : null;

        return (condition.Field, condition.Operator) switch
        {
            (SmartShelfField.Title, SmartShelfOperator.Contains) => filter with { TitleContains = condition.Value },
            (SmartShelfField.Author, SmartShelfOperator.Contains) => filter with { AuthorContains = condition.Value },
            (SmartShelfField.Status, SmartShelfOperator.Equals) when integer.HasValue => filter with { Status = integer },
            (SmartShelfField.Rating, SmartShelfOperator.GreaterThanOrEqual) when integer.HasValue => filter with { MinRating = integer },
            (SmartShelfField.Rating, SmartShelfOperator.LessThanOrEqual) when integer.HasValue => filter with { MaxRating = integer },
            (SmartShelfField.IsAvailable, SmartShelfOperator.Equals or SmartShelfOperator.NotEquals) when flag.HasValue =>
                filter with { IsAvailable = isEquals ? flag.Value : !flag.Value },
            (SmartShelfField.QualityScore, SmartShelfOperator.LessThan)
                when double.TryParse(condition.Value, System.Globalization.NumberStyles.Float, Invariant, out double quality) =>
                filter with { QualityBelow = quality },
            (SmartShelfField.HasAuthor, _) => Missing(filter, condition, flag, CatalogueMissingFields.Author),
            (SmartShelfField.HasIsbn, _) => Missing(filter, condition, flag, CatalogueMissingFields.Isbn),
            (SmartShelfField.HasCover, _) => Missing(filter, condition, flag, CatalogueMissingFields.Cover),
            (SmartShelfField.HasYear, _) => Missing(filter, condition, flag, CatalogueMissingFields.Year),
            (SmartShelfField.NeedsOcr, SmartShelfOperator.Equals) when flag == true => filter with { NeedsOcr = true },
            (SmartShelfField.PossibleDuplicate, SmartShelfOperator.Equals) when flag == true => filter with { PossibleDuplicates = true },
            _ => null,
        };
    }

    private static CatalogueFilter? Missing(
        CatalogueFilter filter,
        SmartShelfCondition condition,
        bool? flag,
        CatalogueMissingFields field)
    {
        // Only "has X = false" (or "has X != true") means "X is missing".
        bool isMissing = (condition.Operator == SmartShelfOperator.Equals && flag == false) ||
                         (condition.Operator == SmartShelfOperator.NotEquals && flag == true);
        return isMissing ? filter with { MissingFields = filter.MissingFields | field } : null;
    }

    private static SmartShelfCondition Flag(SmartShelfField field, bool value) =>
        new(field, SmartShelfOperator.Equals, value ? "true" : "false");

    private static void AddMissing(
        List<SmartShelfCondition> conditions,
        CatalogueMissingFields missing,
        CatalogueMissingFields flag,
        SmartShelfField field)
    {
        if (missing.HasFlag(flag))
        {
            conditions.Add(Flag(field, false));
        }
    }
}
