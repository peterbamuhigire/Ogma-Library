using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Infrastructure.Ocr;

namespace OgmaLibrary.Tests.Ocr;

/// <summary>
/// Sept-23 Phase 17 (task 5): the UI may only offer OCR languages whose trained data ships with
/// a pinned checksum, and the OCR policy settings are stored safely.
/// </summary>
public sealed class Sept23Phase17LanguagePackTests : IDisposable
{
    private static readonly string[] UnshippedLanguages = ["deu", "fra", "ita", "spa"];
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ogma-p17-tessdata-{Guid.NewGuid():N}");

    public Sept23Phase17LanguagePackTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LanguageCatalog_PackagedTessdata_OffersOnlyVerifiedEnglish()
    {
        // Fails before: the policy allowed deu, fra, ita and spa, which never shipped.
        var catalog = new TesseractOcrLanguageCatalog(Path.Combine(AppContext.BaseDirectory, "tessdata"));

        Assert.Equal(["eng"], catalog.GetInstalledLanguages());
        Assert.All(UnshippedLanguages, language => Assert.Null(OcrLanguagePolicy.Normalize(language)));
    }

    [Fact]
    public void LanguageCatalog_MissingOrTamperedTrainingData_OffersNothing()
    {
        Assert.Empty(new TesseractOcrLanguageCatalog(_root).GetInstalledLanguages());

        File.WriteAllBytes(Path.Combine(_root, "eng.traineddata"), "not the pinned file"u8.ToArray());
        Assert.Empty(new TesseractOcrLanguageCatalog(_root).GetInstalledLanguages());
    }

    [Fact]
    public async Task Provider_LanguageNotShipped_FailsWithTypedMissingLanguageData()
    {
        var provider = new TesseractOcrProvider(_root);
        await using var image = new MemoryStream([1, 2, 3]);

        OcrFailureException unsupported = await Assert.ThrowsAsync<OcrFailureException>(
            () => provider.RecognizeAsync(image, "fra"));
        OcrFailureException missing = await Assert.ThrowsAsync<OcrFailureException>(
            () => provider.RecognizeAsync(new MemoryStream([1, 2, 3]), "eng"));

        Assert.Equal(OcrFailureCodes.MissingLanguageData, unsupported.Code);
        Assert.Equal(OcrFailureCodes.MissingLanguageData, missing.Code);
    }

    [Fact]
    public async Task PolicyStore_DefaultsToAutoOcr_RoundTrips_AndRecoversFromCorruption()
    {
        string path = Path.Combine(_root, "ocr-settings.json");
        var store = new JsonOcrPolicySettingsStore(path);

        OcrPolicySettings defaults = await store.GetAsync();
        Assert.True(defaults.AutoOcrScannedBooks);
        Assert.True(defaults.PauseOnBattery);
        Assert.Equal("eng", defaults.Language);

        await store.SaveAsync(new OcrPolicySettings(AutoOcrScannedBooks: false, Language: "fra", PauseOnBattery: false));
        OcrPolicySettings saved = await store.GetAsync();
        Assert.False(saved.AutoOcrScannedBooks);
        Assert.False(saved.PauseOnBattery);
        Assert.Equal("eng", saved.Language); // an unshipped language is never stored
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));

        await File.WriteAllTextAsync(path, "{ not json");
        Assert.Equal(OcrPolicySettings.Default, await store.GetAsync());
    }
}
