using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>
/// Tests for ISBN detection from text and PDF sources (FR-META-001).
/// </summary>
public sealed class IsbnDetectionTests
{
    // â”€â”€ Text extraction helper â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Theory]
    [InlineData("9780262033848")]                        // bare ISBN-13
    [InlineData("ISBN 9780262033848")]                   // with prefix
    [InlineData("ISBN-13: 978-0-262-03384-8")]           // with hyphens
    [InlineData("ISBN: 978 0 262 03384 8")]              // with spaces
    [InlineData("978-0-262-03384-8")]                    // hyphens no prefix
    public void IsbnDetection_ExtractFromText_FindsValidIsbn13(string text)
    {
        var candidates = IsbnDetectionService.ExtractIsbnCandidates(text);

        Assert.NotEmpty(candidates);
        Assert.Contains(candidates, c => c.Normalized == "9780262033848");
    }

    [Theory]
    [InlineData("0262033844")]                           // valid ISBN-10
    [InlineData("ISBN 0-262-03384-4")]                   // with prefix + hyphens
    [InlineData("ISBN: 0262033844")]                     // with prefix
    public void IsbnDetection_ExtractFromText_FindsValidIsbn10(string text)
    {
        var candidates = IsbnDetectionService.ExtractIsbnCandidates(text);

        // ISBN-10 value object stores the digits as-is (no ISBN-13 conversion in value object)
        Assert.NotEmpty(candidates);
        Assert.Contains(candidates, c => c.Normalized == "0262033844");
    }

    [Theory]
    [InlineData("9780262033849")]                        // bad check digit for ISBN-13
    [InlineData("0262033845")]                           // bad check digit for ISBN-10
    [InlineData("ISBN 9780000000001")]                   // invalid
    public void IsbnDetection_InvalidCheckDigit_Rejected(string text)
    {
        var candidates = IsbnDetectionService.ExtractIsbnCandidates(text);

        Assert.Empty(candidates);
    }

    [Fact]
    public void IsbnDetection_MultipleIsbnInText_AllValidOnesExtracted()
    {
        string text = "Book has ISBN 9780262033848 and also ISBN 9780743273565. Invalid: 9780000000000.";

        var candidates = IsbnDetectionService.ExtractIsbnCandidates(text);

        // Both valid ISBNs should be found; the invalid one should not.
        Assert.Contains(candidates, c => c.Normalized == "9780262033848");
        Assert.Contains(candidates, c => c.Normalized == "9780743273565");
        Assert.DoesNotContain(candidates, c => c.Normalized == "9780000000000");
    }

    // â”€â”€ File-based detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task IsbnDetection_FromFilename_DetectsIsbn()
    {
        // Use a temp file whose name contains a valid ISBN.
        string tempPath = Path.Combine(Path.GetTempPath(), "9780262033848-ogma-test.pdf");
        await File.WriteAllBytesAsync(tempPath, MinimalPdfBytes("no isbn here"));

        try
        {
            var service = new IsbnDetectionService();
            IsbnDetectionResult result = await service.DetectAsync(tempPath);

            // Should find the ISBN from the filename.
            Assert.Contains(result.AllCandidates, c => c.Isbn.Normalized == "9780262033848");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task IsbnDetection_NonExistentFile_ReturnsEmptyResult()
    {
        var service = new IsbnDetectionService();
        var result = await service.DetectAsync(@"C:\nonexistent\file.pdf");

        Assert.Null(result.BestIsbn);
        Assert.Empty(result.AllCandidates);
    }

    [Fact]
    public async Task IsbnDetection_FilenameHasIsbnButBadContentFile_StillReturnsFilenameIsbn()
    {
        // Create a minimal PDF that isn't really valid - the ISBN is only in the filename.
        string tempPath = Path.Combine(Path.GetTempPath(), "9780262033848-badcontent.pdf");
        await File.WriteAllTextAsync(tempPath, "not a pdf");

        try
        {
            var service = new IsbnDetectionService();
            var result = await service.DetectAsync(tempPath);

            // Should still detect from filename even when PDF parsing fails.
            Assert.Contains(result.AllCandidates, c => c.Isbn.Normalized == "9780262033848");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    // â”€â”€ Confidence merge formula â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ConfidenceMerge_RecencyBonus_Under7Days_Returns1()
    {
        double bonus = ConfidenceMergeService.ComputeRecencyBonus(DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Equal(1.0, bonus);
    }

    [Fact]
    public void ConfidenceMerge_RecencyBonus_Under30Days_Returns085()
    {
        double bonus = ConfidenceMergeService.ComputeRecencyBonus(DateTimeOffset.UtcNow.AddDays(-14));
        Assert.Equal(0.85, bonus);
    }

    [Fact]
    public void ConfidenceMerge_RecencyBonus_Over30Days_Returns07()
    {
        double bonus = ConfidenceMergeService.ComputeRecencyBonus(DateTimeOffset.UtcNow.AddDays(-45));
        Assert.Equal(0.70, bonus);
    }

    [Fact]
    public void ConfidenceMerge_ProviderWeight_GoogleBooks_Is085()
    {
        double weight = ConfidenceMergeService.GetProviderWeight("GoogleBooks");
        Assert.Equal(0.85, weight);
    }

    [Fact]
    public void ConfidenceMerge_ProviderWeight_OpenLibrary_Is080()
    {
        double weight = ConfidenceMergeService.GetProviderWeight("OpenLibrary");
        Assert.Equal(0.80, weight);
    }

    [Fact]
    public void ConfidenceMerge_MatchScore_ExactIsbn_Returns1()
    {
        double score = ConfidenceMergeService.ComputeMatchScore("ISBN", "9780262033848", "9780262033848");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void ConfidenceMerge_MatchScore_DifferentIsbn_Returns0()
    {
        double score = ConfidenceMergeService.ComputeMatchScore("ISBN", "9780262033848", "9780743273565");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ConfidenceMerge_MatchScore_IdenticalTitle_Returns1()
    {
        double score = ConfidenceMergeService.ComputeMatchScore("Title", "The Art of Reading", "The Art of Reading");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void ConfidenceMerge_MatchScore_NoExistingValue_Returns1()
    {
        double score = ConfidenceMergeService.ComputeMatchScore("Title", "Some Title", null);
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void ConfidenceMerge_UsesFormula_Correctly()
    {
        // FieldConfidence = ProviderWeight Ã— MatchScore Ã— RecencyBonus
        // GoogleBooks=0.85, MatchScore for identical title=1.0, age=1d â†’ RecencyBonus=1.0
        // Expected â‰ˆ 0.85 Ã— 1.0 Ã— 1.0 = 0.85
        double providerWeight = ConfidenceMergeService.GetProviderWeight("GoogleBooks");
        double matchScore = ConfidenceMergeService.ComputeMatchScore("Title", "The Pragmatic Programmer", null);
        double recencyBonus = ConfidenceMergeService.ComputeRecencyBonus(DateTimeOffset.UtcNow.AddHours(-1));
        double fieldConfidence = providerWeight * matchScore * recencyBonus;

        Assert.InRange(fieldConfidence, 0.84, 0.86); // Â±0.01 tolerance
    }

    [Fact]
    public void LevenshteinSimilarity_IdenticalStrings_Returns1()
    {
        double sim = ConfidenceMergeService.NormalizedLevenshteinSimilarity("Hello", "Hello");
        Assert.Equal(1.0, sim);
    }

    [Fact]
    public void LevenshteinSimilarity_CompletelyDifferent_ReturnsLow()
    {
        double sim = ConfidenceMergeService.NormalizedLevenshteinSimilarity("ABC", "XYZ");
        Assert.True(sim < 0.5, $"Expected < 0.5 but got {sim}");
    }

    // â”€â”€ Quality score formula â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task QualityScore_ComputedPerBook_InRange()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        // Seed a book with Title and Author (2 of 7 core fields).
        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "QUAL001",
            Title = "Test Book",
            Status = 0,
        });
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "QUAL001",
            FieldName = "Author",
            Value = "Test Author",
            Source = "PDF",
            Confidence = 0.8,
        });
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "QUAL001",
            FieldName = "ISBN",
            Value = "9780262033848",
            Source = "PDF",
            Confidence = 0.9,
        });
        await context.SaveChangesAsync();

        var svc = new MetadataQualityService(context);
        var score = await svc.RecalculateAsync("QUAL001");

        Assert.InRange(score.QualityScore, 0.0, 1.0);
        Assert.True(score.PresentFieldCount >= 2); // At least Title and Author
        Assert.Equal(7, score.TotalCoreFields);
    }

    [Fact]
    public async Task QualityScore_ChangesAfterMetadataWrite()
    {
        using var context = CatalogueTestHelper.CreateInMemoryContext();

        context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = "QUAL002",
            Status = 0,
        });
        await context.SaveChangesAsync();

        var svc = new MetadataQualityService(context);
        var before = await svc.RecalculateAsync("QUAL002");

        // Add some metadata fields.
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "QUAL002",
            FieldName = "Title",
            Value = "New Title",
            Source = "GoogleBooks",
            Confidence = 0.85,
        });
        context.BookMetadataFields.Add(new Infrastructure.Catalogue.Entities.BookMetadataFieldRow
        {
            BookId = "QUAL002",
            FieldName = "Author",
            Value = "Author Name",
            Source = "GoogleBooks",
            Confidence = 0.85,
        });
        await context.SaveChangesAsync();

        var after = await svc.RecalculateAsync("QUAL002");

        Assert.True(after.QualityScore > before.QualityScore,
            $"Expected score to increase after adding fields. Before={before.QualityScore}, After={after.QualityScore}");
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static byte[] MinimalPdfBytes(string body)
    {
        // Build a minimal PDF using PdfSharp so it's valid enough for tests.
        using var doc = new PdfSharp.Pdf.PdfDocument();
        doc.Info.Subject = body;
        doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }
}

