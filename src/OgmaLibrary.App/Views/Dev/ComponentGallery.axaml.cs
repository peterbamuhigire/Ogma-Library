using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.Views.Dev;

/// <summary>
/// The living component gallery (Sept-23 Phase 09, task 9.4): every token-driven component and
/// state in the current theme, for visual QA. Opened by the hidden Ctrl+Shift+Alt+G shell command.
/// </summary>
public partial class ComponentGallery : UserControl
{
    /// <summary>Initializes the gallery.</summary>
    public ComponentGallery()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Opens the gallery in its own window, inheriting the owner's theme.</summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="localization">The localization service.</param>
    /// <returns>The shown window.</returns>
    public static Window Show(Window owner, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var model = new ComponentGalleryModel(localization);
        var window = new Window
        {
            Title = model.Title,
            Width = 1180,
            Height = 860,
            RequestedThemeVariant = owner.ActualThemeVariant,
            Content = new ComponentGallery { DataContext = model },
        };
        Themes.MotionPreference.Apply(window);
        window.Show(owner);
        return window;
    }
}

/// <summary>Localized labels and sample data for <see cref="ComponentGallery"/>.</summary>
/// <param name="localization">The localization service.</param>
public sealed class ComponentGalleryModel(ILocalizationService localization)
{
    private readonly ILocalizationService _l = localization ?? throw new ArgumentNullException(nameof(localization));

    /// <summary>Gallery title.</summary>
    public string Title => _l["Dev.Gallery.Title"];

    /// <summary>Gallery subtitle.</summary>
    public string Subtitle => _l["Dev.Gallery.Subtitle"];

    /// <summary>Type-scale section heading.</summary>
    public string TypeHeading => _l["Dev.Gallery.Type"];

    /// <summary>Buttons section heading.</summary>
    public string ButtonsHeading => _l["Dev.Gallery.Buttons"];

    /// <summary>Controls section heading.</summary>
    public string ControlsHeading => _l["Dev.Gallery.Controls"];

    /// <summary>Covers section heading.</summary>
    public string CoversHeading => _l["Dev.Gallery.Covers"];

    /// <summary>Icons section heading.</summary>
    public string IconsHeading => _l["Dev.Gallery.Icons"];

    /// <summary>Sample display text.</summary>
    public string SampleTitle => _l["Dev.Gallery.SampleTitle"];

    /// <summary>Sample body text.</summary>
    public string SampleBody => _l["Dev.Gallery.SampleBody"];

    /// <summary>Sample mono (data) text.</summary>
    public string SampleMono => _l["Dev.Gallery.SampleMono"];

    /// <summary>Sample author for covers.</summary>
    public string SampleAuthor => _l["Dev.Gallery.SampleAuthor"];

    /// <summary>Variant label.</summary>
    public string Primary => _l["Dev.Gallery.Primary"];

    /// <summary>Variant label.</summary>
    public string Secondary => _l["Dev.Gallery.Secondary"];

    /// <summary>Variant label.</summary>
    public string Tool => _l["Dev.Gallery.Tool"];

    /// <summary>Variant label.</summary>
    public string Danger => _l["Dev.Gallery.Danger"];

    /// <summary>Variant label.</summary>
    public string Link => _l["Dev.Gallery.Link"];

    /// <summary>State label.</summary>
    public string Active => _l["Dev.Gallery.Active"];

    /// <summary>State label.</summary>
    public string Busy => _l["Dev.Gallery.Busy"];

    /// <summary>State label.</summary>
    public string Compact => _l["Dev.Gallery.Compact"];

    /// <summary>State label.</summary>
    public string Disabled => _l["Dev.Gallery.Disabled"];

    /// <summary>Segment label.</summary>
    public string Grid => _l["Icon.ic_cat_view_grid.Label"];

    /// <summary>Segment label.</summary>
    public string List => _l["Icon.ic_cat_view_list.Label"];

    /// <summary>Segment label.</summary>
    public string Directory => _l["Icon.ic_cat_view_directory.Label"];

    /// <summary>Status label.</summary>
    public string Success => _l["Dev.Gallery.Success"];

    /// <summary>Status label.</summary>
    public string Warning => _l["Dev.Gallery.Warning"];

    /// <summary>Status label.</summary>
    public string Info => _l["Dev.Gallery.Info"];

    /// <summary>Status label.</summary>
    public string Ai => _l["Dev.Gallery.Ai"];

    /// <summary>Status label.</summary>
    public string Neutral => _l["Dev.Gallery.Neutral"];

    /// <summary>Sample titles that exercise all cloth colours and weaves.</summary>
    public IReadOnlyList<string> CoverTitles { get; } =
    [
        "Middlemarch", "The Lantern Keeper", "Algorithms Explained", "Trade Routes of East Africa",
        "A Short History of the Great Lakes", "Introduction to Tropical Ecology", "The Physics of Everyday Light",
        "Big Reference Handbook",
    ];

    /// <summary>Every registered icon path.</summary>
    public IReadOnlyList<string> IconPaths { get; } =
        [.. IconCatalog.AllKeys.Select(key => IconCatalog.GetAvaresPath(key)!)];
}
