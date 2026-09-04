namespace OgmaLibrary.Application.Search;

/// <summary>
/// Parses the bounded FTS5 highlight markers into plain text and ranges. Only
/// the markers emitted by SQLite are interpreted; all other source text is
/// retained as text and is therefore safe for ordinary UI text controls.
/// </summary>
public static class SearchSnippetParser
{
    private const string OpenMarker = "<b>";
    private const string CloseMarker = "</b>";

    /// <summary>Converts an FTS snippet into safe plain text and match spans.</summary>
    public static SearchSnippet Parse(string? markedText)
    {
        if (string.IsNullOrEmpty(markedText))
        {
            return new SearchSnippet(string.Empty, []);
        }

        var text = new System.Text.StringBuilder(markedText.Length);
        var spans = new List<SearchSnippetSpan>();
        int? matchStart = null;
        for (int index = 0; index < markedText.Length;)
        {
            if (IsMarkerAt(markedText, index, OpenMarker))
            {
                matchStart ??= text.Length;
                index += OpenMarker.Length;
                continue;
            }

            if (IsMarkerAt(markedText, index, CloseMarker))
            {
                if (matchStart is int start && text.Length > start)
                {
                    spans.Add(new SearchSnippetSpan(start, text.Length - start));
                }

                matchStart = null;
                index += CloseMarker.Length;
                continue;
            }

            text.Append(markedText[index++]);
        }

        if (matchStart is int trailingStart && text.Length > trailingStart)
        {
            spans.Add(new SearchSnippetSpan(trailingStart, text.Length - trailingStart));
        }

        return new SearchSnippet(text.ToString(), spans);
    }

    private static bool IsMarkerAt(string value, int index, string marker) =>
        index + marker.Length <= value.Length &&
        value.AsSpan(index, marker.Length).Equals(marker, StringComparison.OrdinalIgnoreCase);
}
