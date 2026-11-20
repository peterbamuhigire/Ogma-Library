using System.Globalization;
using System.Text.Json;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Reader.Annotations;

namespace OgmaLibrary.Tests.Reader.Citations;

/// <summary>Golden-file tests for the citation exports (Sept-23 Phase 12, READ-013).</summary>
public sealed class CitationFormatterTests
{
    private static readonly CitationCard Sample = new(
        "book-123",
        "Élan & Grace: 100% of the \"Craft\"",
        "Marie Dupré",
        42,
        "A line with {braces}, an underscore_here and\na second line.");

    public static TheoryData<CitationFormat, string> Goldens => new()
    {
        { CitationFormat.BibTeX, "sample.bib" },
        { CitationFormat.Ris, "sample.ris" },
        { CitationFormat.CslJson, "sample.json" },
        { CitationFormat.Markdown, "sample.md" },
    };

    [Theory]
    [MemberData(nameof(Goldens))]
    public void Format_MatchesGoldenFile(CitationFormat format, string file)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Reader", "Citations", "golden", file);
        string expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        string actual = CitationFormatter.Format(Sample, format);
        if (Environment.GetEnvironmentVariable("OGMA_UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(Path.Combine(SourceDirectory(), "golden", file), actual);
            return;
        }

        Assert.Equal(expected, actual);
    }

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;

    [Fact]
    public void Format_IsCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            string french = CitationFormatter.Format(Sample, CitationFormat.Ris);
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal(CitationFormatter.Format(Sample, CitationFormat.Ris), french);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// No CSL JSON schema is vendored in this repository (and the build has no network), so this
    /// checks the keys the CSL-JSON specification requires of an item: <c>id</c> and <c>type</c>,
    /// plus the types of the optional keys the formatter writes.
    /// </summary>
    [Fact]
    public void CslJson_HasRequiredKeysWithSchemaTypes()
    {
        using JsonDocument document = JsonDocument.Parse(CitationFormatter.Format(Sample, CitationFormat.CslJson));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        JsonElement item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(JsonValueKind.String, item.GetProperty("id").ValueKind);
        Assert.Equal("book", item.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.String, item.GetProperty("title").ValueKind);
        Assert.Equal(JsonValueKind.String, item.GetProperty("page").ValueKind);
        Assert.Equal(JsonValueKind.String, item.GetProperty("note").ValueKind);
        JsonElement author = Assert.Single(item.GetProperty("author").EnumerateArray());
        Assert.Equal("Marie Dupré", author.GetProperty("literal").GetString());
    }

    [Fact]
    public void PlainText_IsUnchangedFromTheOriginalFormat() =>
        Assert.Equal(Sample.ToPlainText(), CitationFormatter.Format(Sample, CitationFormat.PlainText));
}
