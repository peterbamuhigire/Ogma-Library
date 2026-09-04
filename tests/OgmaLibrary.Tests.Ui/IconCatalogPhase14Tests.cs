using OgmaLibrary.App.Icons;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Tests;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Icon and localization checks for Phase 14 3D Bookshelf assets.</summary>
public sealed class IconCatalogPhase14Tests
{
    private static readonly string[] Phase14IconKeys =
    [
        "ic_shelf3d_toggle",
        "ic_shelf3d_layout_shelf",
        "ic_shelf3d_layout_grid3d",
        "ic_shelf3d_camera_reset",
        "ic_shelf3d_camera_orbit",
        "ic_shelf3d_camera_zoom",
        "ic_shelf3d_theme_light",
        "ic_shelf3d_theme_dark",
        "ic_shelf3d_unavailable",
        "ic_shelf3d_loading",
        "ic_shelf3d_book_focused",
    ];

    [Fact]
    public void IconCatalog_Phase14ManifestKeys_AllResolve()
    {
        foreach (string key in Phase14IconKeys)
        {
            string? path = IconCatalog.GetAvaresPath(key);

            Assert.NotNull(path);
            Assert.True(File.Exists(ToPhysicalIconPath(path)), $"Missing physical icon asset for {key}: {path}");
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void IconCatalog_Phase14ManifestKeys_HaveAccessibleLabels(string culture)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture(culture);

        foreach (string key in Phase14IconKeys)
        {
            IconEntry? entry = IconCatalog.Resolve(key, localization);

            Assert.NotNull(entry);
            Assert.DoesNotContain("Icon.", entry!.AccessibleLabel, StringComparison.Ordinal);
            Assert.False(entry.AccessibleLabel.StartsWith('\u27E6'));
            Assert.False(string.IsNullOrWhiteSpace(entry.AccessibleLabel));
        }
    }

    private static string ToPhysicalIconPath(string avaresPath)
    {
        const string prefix = "avares://OgmaLibrary.App/";
        Assert.StartsWith(prefix, avaresPath, StringComparison.Ordinal);
        string relative = avaresPath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(RepositoryTestPaths.Root, "src", "OgmaLibrary.App", relative);
    }
}
