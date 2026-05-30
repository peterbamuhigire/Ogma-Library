namespace OgmaLibrary.Tests.GoldenCorpus;

/// <summary>
/// A single synthetic catalogue entry produced by <see cref="SyntheticCorpusGenerator"/>.
/// </summary>
/// <param name="Title">The generated title.</param>
/// <param name="Author">The generated author.</param>
/// <param name="Year">The generated publication year.</param>
/// <param name="PageText">Deterministic per-page pseudo-text used to exercise search.</param>
public sealed record SyntheticBook(string Title, string Author, int Year, IReadOnlyList<string> PageText);

/// <summary>
/// Generates a deterministic synthetic catalogue corpus from a seed, used to build the
/// 500-/2,000-book performance corpora without redistributing any third-party PDF
/// (CON-9). The same seed always yields byte-identical output so benchmark and harness
/// runs are reproducible (Test Strategy §2.3).
/// </summary>
public static class SyntheticCorpusGenerator
{
    private static readonly string[] Adjectives =
        ["Hidden", "Ancient", "Silent", "Golden", "Distant", "Quiet", "Bright", "Wandering"];

    private static readonly string[] Nouns =
        ["Library", "Atlas", "Compendium", "Chronicle", "Almanac", "Treatise", "Lexicon", "Cartography"];

    private static readonly string[] Surnames =
        ["Achebe", "Borges", "Calvino", "Dench", "Eco", "Foucault", "Ginzburg", "Hofstadter"];

    private static readonly string[] Vocabulary =
        ["knowledge", "wisdom", "truth", "history", "science", "author", "publisher", "library",
         "book", "reading", "ogma", "catalogue", "shelf", "annotation", "discovery", "memory"];

    /// <summary>Generates <paramref name="count"/> synthetic books deterministically.</summary>
    /// <param name="count">The number of books to generate.</param>
    /// <param name="seed">The PRNG seed; the same seed yields identical output.</param>
    /// <param name="pagesPerBook">The number of pseudo-text pages per book.</param>
    /// <param name="wordsPerPage">The number of words per page.</param>
    /// <returns>The generated corpus.</returns>
    public static IReadOnlyList<SyntheticBook> Generate(
        int count, int seed, int pagesPerBook = 3, int wordsPerPage = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var random = new Random(seed);
        var books = new List<SyntheticBook>(count);

        for (int i = 0; i < count; i++)
        {
            string title = $"The {Pick(random, Adjectives)} {Pick(random, Nouns)} {i + 1}";
            string author = $"{(char)('A' + random.Next(26))}. {Pick(random, Surnames)}";
            int year = 1950 + random.Next(75);

            var pages = new List<string>(pagesPerBook);
            for (int p = 0; p < pagesPerBook; p++)
            {
                var words = new string[wordsPerPage];
                for (int w = 0; w < wordsPerPage; w++)
                {
                    words[w] = Pick(random, Vocabulary);
                }

                pages.Add(string.Join(' ', words));
            }

            books.Add(new SyntheticBook(title, author, year, pages));
        }

        return books;
    }

    private static string Pick(Random random, string[] source) => source[random.Next(source.Length)];
}
