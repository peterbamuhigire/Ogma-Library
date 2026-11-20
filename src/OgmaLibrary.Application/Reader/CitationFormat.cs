namespace OgmaLibrary.Application.Reader;

/// <summary>The output formats a citation card can be exported or copied in (READ-013).</summary>
public enum CitationFormat
{
    /// <summary>One plain-text line: quote, author, title, page.</summary>
    PlainText = 0,

    /// <summary>A BibTeX <c>@misc</c> entry.</summary>
    BibTeX = 1,

    /// <summary>An RIS record.</summary>
    Ris = 2,

    /// <summary>A CSL-JSON array with one item.</summary>
    CslJson = 3,

    /// <summary>A Markdown block quote with an attribution line.</summary>
    Markdown = 4,
}

/// <summary>File-name facts about a <see cref="CitationFormat"/>.</summary>
public static class CitationFormatInfo
{
    /// <summary>Gets the file extension, without the dot, used when saving the format.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The extension.</returns>
    public static string Extension(CitationFormat format) => format switch
    {
        CitationFormat.BibTeX => "bib",
        CitationFormat.Ris => "ris",
        CitationFormat.CslJson => "json",
        CitationFormat.Markdown => "md",
        _ => "txt",
    };
}
