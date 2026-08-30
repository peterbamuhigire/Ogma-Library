using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>Phase 24 deterministic selective-OCR policy tests.</summary>
public sealed class Phase24OcrQualityTests
{
    [Fact]
    public void TextPage_IsNotSelectedForOcr()
    {
        Assert.False(OcrPageQualityPolicy.ShouldProcess(ExtractionQuality.Full, 300));
        Assert.False(OcrPageQualityPolicy.ShouldProcess(ExtractionQuality.Partial, 12));
    }

    [Fact]
    public void ScannedAndLowWordPages_AreSelectedForOcr()
    {
        Assert.True(OcrPageQualityPolicy.ShouldProcess(ExtractionQuality.Scanned, 0));
        Assert.True(OcrPageQualityPolicy.ShouldProcess(ExtractionQuality.Empty, 0));
        Assert.True(OcrPageQualityPolicy.ShouldProcess(ExtractionQuality.Partial, 11));
    }

    [Fact]
    public void OcrSelection_RequiresConfidenceAndDoesNotReplaceGoodPrimaryText()
    {
        Assert.True(OcrPageQualityPolicy.ShouldSelectOcr(
            SearchExtractionQuality.Empty,
            0,
            "recognized text",
            0.75));
        Assert.False(OcrPageQualityPolicy.ShouldSelectOcr(
            SearchExtractionQuality.Empty,
            0,
            "uncertain text",
            0.74));
        Assert.False(OcrPageQualityPolicy.ShouldSelectOcr(
            SearchExtractionQuality.Full,
            300,
            "recognized text",
            0.99));
    }

    [Fact]
    public void LanguagePolicy_OnlyAllowsKnownLocalPacks()
    {
        Assert.Equal("eng", OcrLanguagePolicy.Normalize("ENG"));
        Assert.Equal("eng+fra", OcrLanguagePolicy.Normalize("fra+eng"));
        Assert.Null(OcrLanguagePolicy.Normalize("eng;delete-all"));
        Assert.Null(OcrLanguagePolicy.Normalize("jpn"));
    }
}
