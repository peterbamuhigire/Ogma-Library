using OgmaLibrary.App.Icons;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Phase 10 icon manifest coverage for search and Index Manager surfaces.</summary>
public sealed class IconCatalogPhase10Tests
{
    private static readonly string[] Phase10IconKeys =
    [
        "ic_search_global",
        "ic_search_clear",
        "ic_search_result_book",
        "ic_search_no_results",
        "ic_search_filter",
        "ic_filter_chip_page",
        "ic_filter_chip_note",
        "ic_filter_chip_tag",
        "ic_filter_chip_toc",
        "ic_filter_chip_description",
        "ic_index_manager",
        "ic_index_status_indexed",
        "ic_index_status_indexing",
        "ic_index_status_pending_ocr",
        "ic_index_status_failed",
        "ic_index_rebuild",
        "ic_index_rebuild_cancel",
        "ic_index_size",
    ];

    [Fact]
    public void IconCatalog_Phase10ManifestKeys_AllResolve()
    {
        foreach (string key in Phase10IconKeys)
        {
            string? path = IconCatalog.GetAvaresPath(key);

            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.StartsWith("avares://OgmaLibrary.App/Assets/icons/", path, StringComparison.Ordinal);
            Assert.Contains($"/{key}.svg", path, StringComparison.Ordinal);
            Assert.True(File.Exists(ToPhysicalIconPath(path)), $"Missing physical icon asset for {key}: {path}");
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void IconCatalog_Phase10ManifestKeys_HaveAccessibleLabels(string culture)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture(culture);

        foreach (string key in Phase10IconKeys)
        {
            IconEntry? entry = IconCatalog.Resolve(key, localization);

            Assert.NotNull(entry);
            Assert.False(entry.AccessibleLabel.StartsWith('\u27E6'));
            Assert.False(string.IsNullOrWhiteSpace(entry.AccessibleLabel));
        }
    }

    private static string ToPhysicalIconPath(string avaresPath)
    {
        const string prefix = "avares://OgmaLibrary.App/";
        string relative = avaresPath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OgmaLibrary.App", relative));
    }
}
