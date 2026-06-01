namespace OgmaLibrary.Application.Search;

/// <summary>
/// Splits extracted text into deterministic, token-bounded chunks for FTS5 and
/// future embedding generation. The defaults are Phase 10's 512-token chunks
/// with 64-token overlap.
/// </summary>
public sealed class SearchChunker
{
    /// <summary>Default maximum number of tokens in one search chunk.</summary>
    public const int DefaultMaxTokens = 512;

    /// <summary>Default number of tokens repeated between adjacent chunks.</summary>
    public const int DefaultOverlapTokens = 64;

    private readonly int _defaultMaxTokens;
    private readonly int _defaultOverlapTokens;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchChunker"/>.
    /// </summary>
    public SearchChunker(
        int defaultMaxTokens = DefaultMaxTokens,
        int defaultOverlapTokens = DefaultOverlapTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultMaxTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(defaultOverlapTokens);
        if (defaultOverlapTokens >= defaultMaxTokens)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultOverlapTokens),
                "Chunk overlap must be smaller than the maximum chunk size.");
        }

        _defaultMaxTokens = defaultMaxTokens;
        _defaultOverlapTokens = defaultOverlapTokens;
    }

    /// <summary>
    /// Splits one source text into ordered search chunks.
    /// </summary>
    public IReadOnlyList<SearchChunkRecord> Chunk(
        string bookId,
        SearchChunkSource source,
        string? text,
        int startingChunkIndex,
        DateTimeOffset createdAtUtc,
        long? extractedPageId = null,
        int? pageIndex = null,
        int? maxTokens = null,
        int? overlapTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentOutOfRangeException.ThrowIfNegative(startingChunkIndex);

        int effectiveMaxTokens = maxTokens ?? _defaultMaxTokens;
        int effectiveOverlapTokens = overlapTokens ?? _defaultOverlapTokens;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveMaxTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(effectiveOverlapTokens);

        if (effectiveOverlapTokens >= effectiveMaxTokens)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapTokens),
                "Chunk overlap must be smaller than the maximum chunk size.");
        }

        string[] tokens = Tokenize(text);
        if (tokens.Length == 0)
        {
            return [];
        }

        var chunks = new List<SearchChunkRecord>();
        int step = effectiveMaxTokens - effectiveOverlapTokens;
        int offset = 0;
        int chunkIndex = startingChunkIndex;

        while (offset < tokens.Length)
        {
            int count = Math.Min(effectiveMaxTokens, tokens.Length - offset);
            string chunkText = string.Join(' ', tokens.AsSpan(offset, count).ToArray());
            chunks.Add(new SearchChunkRecord(
                Id: 0,
                BookId: bookId,
                ExtractedPageId: extractedPageId,
                PageIndex: pageIndex,
                ChunkIndex: chunkIndex,
                Text: chunkText,
                TokenCount: count,
                Source: source,
                CreatedAtUtc: createdAtUtc));

            if (offset + count >= tokens.Length)
            {
                break;
            }

            offset += step;
            chunkIndex++;
        }

        return chunks;
    }

    /// <summary>
    /// Counts approximate tokens using the same whitespace tokenization as
    /// <see cref="Chunk"/>.
    /// </summary>
    public static int CountTokens(string? text) => Tokenize(text).Length;

    private static string[] Tokenize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
