using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Ocr;

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

    [Fact]
    public void TrainingDataVerifier_RequiresApprovedChecksum()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-tessdata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "eng.traineddata"), "training-data"u8.ToArray());

            TesseractTrainingDataVerification result =
                TesseractTrainingDataVerifier.Verify(root, "eng");

            Assert.False(result.IsValid);
            Assert.Equal("TrainingDataChecksumMismatch", result.Code);
            Assert.Equal("eng", result.Language);
            Assert.NotNull(result.ActualSha256);
            Assert.NotEqual(result.ActualSha256, result.ExpectedSha256);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TrainingDataVerifier_AcceptsRestoredEnglishPack()
    {
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");

        TesseractTrainingDataVerification result =
            TesseractTrainingDataVerifier.Verify(tessdataPath, "eng");

        Assert.True(result.IsValid);
        Assert.Equal("Verified", result.Code);
    }

    [Fact]
    public void TrainingDataVerifier_FailsClosedForUnapprovedLanguagePack()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-tessdata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            TesseractTrainingDataVerification result =
                TesseractTrainingDataVerifier.Verify(root, "fra");

            Assert.False(result.IsValid);
            Assert.Equal("MissingApprovedChecksum", result.Code);
            Assert.Equal("fra", result.Language);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
