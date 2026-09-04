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
                    condition.Value.Length > MaxValueLength))
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
        double? fieldValue = GetFieldValue(book, condition.Field);

        if (!double.TryParse(condition.Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double conditionValue))
        {
            // For boolean fields the value is "true"/"false"
            if (condition.Field == SmartShelfField.IsAvailable)
            {
                bool boolCondition = condition.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                bool boolField = book.IsAvailable;
                return condition.Operator switch
                {
                    SmartShelfOperator.Equals => boolField == boolCondition,
                    SmartShelfOperator.NotEquals => boolField != boolCondition,
                    _ => false,
                };
            }

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
            SmartShelfField.IsAvailable => book.IsAvailable ? 1.0 : 0.0,
            SmartShelfField.ReadingProgressPct => book.ReadingProgressPct,
            _ => null,
        };
}
