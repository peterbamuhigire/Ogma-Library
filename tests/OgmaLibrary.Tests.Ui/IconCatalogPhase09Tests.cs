using System.Xml.Linq;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Phase 09 icon manifest coverage for annotation reader surfaces.</summary>
public sealed class IconCatalogPhase09Tests
{
    private static readonly string[] Phase09IconKeys =
    [
        "ic_annotation_highlight",
        "ic_annotation_highlight_color",
        "ic_annotation_note",
        "ic_annotation_note_anchor",
        "ic_annotation_delete",
        "ic_bookmark_add",
        "ic_bookmark_remove",
        "ic_bookmark_panel",
        "ic_bookmark_item",
        "ic_bookmark_rename",
        "ic_layer_panel",
        "ic_layer_add",
        "ic_layer_visible",
        "ic_layer_hidden",
        "ic_layer_merge",
        "ic_layer_delete",
        "ic_citation_capture",
        "ic_citation_copy",
        "ic_citation_export",
        "ic_reading_memory",
        "ic_reading_memory_disposition",
        "ic_annotation_panel",
    ];

    private static readonly string[] Phase09ResourceKeys =
    [
        "Annotation.Highlight.Create",
        "Annotation.Highlight.Color",
        "Annotation.Highlight.LayerColor",
        "Annotation.Note.Create",
        "Annotation.Note.Anchor",
        "Annotation.Note.Edit",
        "Annotation.Note.Editor",
        "Annotation.Note.Saved",
        "Annotation.Delete",
        "Annotation.Panel",
        "Annotation.Saved",
        "Annotation.Deleted",
        "Annotation.SelectionQuoteFormat",
        "Annotation.SampleQuoteFormat",
        "Annotation.SampleNoteFormat",
        "Annotation.AccessibleLabelWithLayerFormat",
        "Annotation.AccessibleLabelWithoutLayerFormat",
        "Annotation.Delete.Confirm",
        "Annotation.Delete.Cancel",
        "Annotation.Delete.Confirmation",
        "Bookmark.Add",
        "Bookmark.Remove",
        "Bookmark.Panel",
        "Bookmark.Item",
        "Bookmark.Rename",
        "Bookmark.Renamed",
        "Bookmark.Saved",
        "Bookmark.Removed",
        "Bookmark.DefaultLabelFormat",
        "Bookmark.Panel.AccessibleFormat",
        "Layer.Panel",
        "Layer.Filter",
        "Layer.Filter.AllVisible",
        "Layer.Add",
        "Layer.DefaultName",
        "Layer.DefaultNameFormat",
        "Layer.Visible",
        "Layer.Hidden",
        "Layer.Merge",
        "Layer.Delete",
        "Layer.Renamed",
        "Layer.Deleted",
        "Layer.Merged",
        "Layer.AtLeastOne",
        "Citation.Capture",
        "Citation.Copy",
        "Citation.Export",
        "Citation.Copied",
        "Citation.Exported",
        "Citation.NoSelection",
        "Citation.PageFormat",
        "Citation.UnknownTitle",
        "Citation.UnknownAuthor",
        "ReadingMemory.Open",
        "ReadingMemory.Disposition",
        "ReadingMemory.OpenedBecause",
        "ReadingMemory.KeyInsight",
        "ReadingMemory.OpenQuestions",
        "ReadingMemory.Saved",
        "ReadingMemory.Save",
        "ReadingMemory.InvalidDisposition",
        "Icon.ic_annotation_highlight.Label",
        "Icon.ic_annotation_highlight_color.Label",
        "Icon.ic_annotation_note.Label",
        "Icon.ic_annotation_note_anchor.Label",
        "Icon.ic_annotation_delete.Label",
        "Icon.ic_annotation_panel.Label",
        "Icon.ic_bookmark_add.Label",
        "Icon.ic_bookmark_remove.Label",
        "Icon.ic_bookmark_panel.Label",
        "Icon.ic_bookmark_item.Label",
        "Icon.ic_bookmark_rename.Label",
        "Icon.ic_layer_panel.Label",
        "Icon.ic_layer_add.Label",
        "Icon.ic_layer_visible.Label",
        "Icon.ic_layer_hidden.Label",
        "Icon.ic_layer_merge.Label",
        "Icon.ic_layer_delete.Label",
        "Icon.ic_citation_capture.Label",
        "Icon.ic_citation_copy.Label",
        "Icon.ic_citation_export.Label",
        "Icon.ic_reading_memory.Label",
        "Icon.ic_reading_memory_disposition.Label",
    ];

    [Fact]
    public void IconCatalog_Phase09ManifestKeys_AllResolve()
    {
        foreach (string key in Phase09IconKeys)
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
    public void IconCatalog_Phase09ManifestKeys_HaveAccessibleLabels(string culture)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture(culture);

        foreach (string key in Phase09IconKeys)
        {
            IconEntry? entry = IconCatalog.Resolve(key, localization);

            Assert.NotNull(entry);
            Assert.False(entry.AccessibleLabel.StartsWith('\u27E6'));
            Assert.False(string.IsNullOrWhiteSpace(entry.AccessibleLabel));
        }
    }

    [Theory]
    [InlineData("annotations.en.resx")]
    [InlineData("annotations.fr.resx")]
    public void Phase09AnnotationResources_ContainRequiredKeys(string fileName)
    {
        Dictionary<string, string> resources = LoadPhase09Resources(fileName);

        foreach (string key in Phase09ResourceKeys)
        {
            Assert.True(resources.TryGetValue(key, out string? value), $"Missing {key} in {fileName}.");
            Assert.False(string.IsNullOrWhiteSpace(value), $"Empty {key} in {fileName}.");
        }
    }

    private static Dictionary<string, string> LoadPhase09Resources(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "OgmaLibrary.App",
            "Assets",
            "Strings",
            fileName));

        XDocument document = XDocument.Load(path);
        return document
            .Root!
            .Elements("data")
            .Where(static element => element.Attribute("name") is not null)
            .ToDictionary(
                static element => element.Attribute("name")!.Value,
                static element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string ToPhysicalIconPath(string avaresPath)
    {
        const string Prefix = "avares://OgmaLibrary.App/Assets/icons/";
        string relativePath = avaresPath[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "OgmaLibrary.App",
            "Assets",
            "icons",
            relativePath));
    }
}
