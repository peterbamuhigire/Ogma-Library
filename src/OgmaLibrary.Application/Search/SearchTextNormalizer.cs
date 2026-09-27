using System.Globalization;
using System.Text;

namespace OgmaLibrary.Application.Search;

/// <summary>
/// Case and diacritic folding for search matching (Sept-23 Phase 13). Display text keeps
/// its original form; only comparisons use the folded form, so <c>Wanjirũ</c>,
/// <c>WANJIRU</c> and <c>wanjiru</c> are equal for matching.
/// </summary>
public static class SearchTextNormalizer
{
    /// <summary>
    /// Folds <paramref name="value"/> for matching: compatibility decomposition (NFKD),
    /// combining marks removed, invariant lower case, and the few Latin letters that do not
    /// decompose (ß, æ, œ, ø, đ, ł, ı) mapped to their plain spellings.
    /// </summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string decomposed = value.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char ch in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            char lower = char.ToLowerInvariant(ch);
            switch (lower)
            {
                case 'ß':
                    builder.Append("ss");
                    break;
                case 'æ':
                    builder.Append("ae");
                    break;
                case 'œ':
                    builder.Append("oe");
                    break;
                case 'ø':
                    builder.Append('o');
                    break;
                case 'đ':
                    builder.Append('d');
                    break;
                case 'ł':
                    builder.Append('l');
                    break;
                case 'ı':
                    builder.Append('i');
                    break;
                default:
                    builder.Append(lower);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Splits folded text into letter-or-digit tokens (the same word boundaries as the
    /// SQLite <c>unicode61</c> tokenizer uses for the full-text index).
    /// </summary>
    public static IReadOnlyList<string> Tokens(string? value)
    {
        string folded = Fold(value);
        if (folded.Length == 0)
        {
            return [];
        }

        var tokens = new List<string>();
        int start = -1;
        for (int index = 0; index <= folded.Length; index++)
        {
            bool isWordChar = index < folded.Length && char.IsLetterOrDigit(folded[index]);
            if (isWordChar && start < 0)
            {
                start = index;
            }
            else if (!isWordChar && start >= 0)
            {
                tokens.Add(folded[start..index]);
                start = -1;
            }
        }

        return tokens;
    }

    /// <summary>
    /// The largest edit distance tolerated for a typo in a term of <paramref name="length"/>
    /// characters: 0 up to 3 characters, 1 for 4–7 and 2 from 8 (plan §11).
    /// </summary>
    public static int TypoBudget(int length) => length switch
    {
        <= 3 => 0,
        <= 7 => 1,
        _ => 2,
    };

    /// <summary>
    /// Optimal-string-alignment (Damerau) edit distance, stopping early once it exceeds
    /// <paramref name="limit"/>; returns <c>limit + 1</c> in that case.
    /// </summary>
    public static int EditDistance(string source, string target, int limit)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (Math.Abs(source.Length - target.Length) > limit)
        {
            return limit + 1;
        }

        int[] twoBack = new int[target.Length + 1];
        int[] previous = new int[target.Length + 1];
        int[] current = new int[target.Length + 1];
        for (int column = 0; column <= target.Length; column++)
        {
            previous[column] = column;
        }

        for (int row = 1; row <= source.Length; row++)
        {
            current[0] = row;
            int rowMinimum = current[0];
            for (int column = 1; column <= target.Length; column++)
            {
                int cost = source[row - 1] == target[column - 1] ? 0 : 1;
                int value = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
                if (row > 1 && column > 1 &&
                    source[row - 1] == target[column - 2] &&
                    source[row - 2] == target[column - 1])
                {
                    value = Math.Min(value, twoBack[column - 2] + 1);
                }

                current[column] = value;
                rowMinimum = Math.Min(rowMinimum, value);
            }

            if (rowMinimum > limit)
            {
                return limit + 1;
            }

            (twoBack, previous, current) = (previous, current, twoBack);
        }

        return Math.Min(previous[target.Length], limit + 1);
    }
}
