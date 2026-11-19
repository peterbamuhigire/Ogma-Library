using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 Phase 10 (task 10.12): the localisation key-parity gate. English and French carry
/// exactly the same keys and no value is empty, so no string falls back to a raw key marker.
/// </summary>
public sealed class LocalizationKeyParityTests
{
    [Fact]
    public void EnglishAndFrench_HaveTheSameKeys()
    {
        var english = InMemoryLocalizationService.EnglishStrings.Keys.ToHashSet(StringComparer.Ordinal);
        var french = InMemoryLocalizationService.FrenchStrings.Keys.ToHashSet(StringComparer.Ordinal);

        string[] missingInFrench = english.Except(french).Order(StringComparer.Ordinal).ToArray();
        string[] missingInEnglish = french.Except(english).Order(StringComparer.Ordinal).ToArray();

        Assert.True(missingInFrench.Length == 0, "Missing in French: " + string.Join(", ", missingInFrench));
        Assert.True(missingInEnglish.Length == 0, "Missing in English: " + string.Join(", ", missingInEnglish));
    }

    [Fact]
    public void NoValueIsEmpty()
    {
        string[] empty = InMemoryLocalizationService.EnglishStrings
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => "en:" + pair.Key)
            .Concat(InMemoryLocalizationService.FrenchStrings
                .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => "fr:" + pair.Key))
            .ToArray();

        Assert.True(empty.Length == 0, "Empty values: " + string.Join(", ", empty));
    }

    [Fact]
    public void NoValueIsMisEncoded()
    {
        // UTF-8 read as Windows-1252 ("IndexÃ©") once reached French badges (K119).
        string[] misEncoded = InMemoryLocalizationService.EnglishStrings
            .Concat(InMemoryLocalizationService.FrenchStrings)
            .Where(pair => pair.Value.Contains('Ã', StringComparison.Ordinal) ||
                           pair.Value.Contains("â€", StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();

        Assert.True(misEncoded.Length == 0, "Mis-encoded values: " + string.Join(", ", misEncoded));
    }

    [Fact]
    public void Phase10Text_HasNoHardCodedEnglishFallback()
    {
        // Without a localisation service the filter shows the resource key, never English.
        var unlocalised = new OgmaLibrary.App.ViewModels.Catalogue.CatalogueFilterViewModel { SortAscending = false };
        Assert.Equal("Catalogue.Sort.Descending", unlocalised.SortDirectionText);

        var french = new InMemoryLocalizationService();
        french.SetCulture("fr");
        var localised = new OgmaLibrary.App.ViewModels.Catalogue.CatalogueFilterViewModel(french) { SortAscending = false };
        Assert.Equal(InMemoryLocalizationService.FrenchStrings["Catalogue.Sort.Descending"], localised.SortDirectionText);

        // The projection leaves the "Unknown author" wording to the localised view layer.
        var authorless = new OgmaLibrary.Application.Catalogue.BookSummaryProjection(
            "x", "Untitled", [" "], null, 0, null, [], null, true, null);
        Assert.Null(authorless.PrimaryAuthor);
    }
}
