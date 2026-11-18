using Avalonia.Controls;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.Themes;

/// <summary>
/// Publishes the few localized strings that control templates need (they have no view model)
/// as application resources, so a template can use <c>{DynamicResource}</c> and follow a
/// culture change. Each resource key equals its localization key.
/// </summary>
public static class StyleStrings
{
    /// <summary>Accessible name and tooltip of the tab strip's "More tabs" overflow button.</summary>
    public const string TabStripMoreKey = "Controls.TabStrip.More";

    private static readonly string[] Keys = [TabStripMoreKey];

    /// <summary>Writes every template string for the current culture into <paramref name="resources"/>.</summary>
    /// <param name="resources">The application resource dictionary.</param>
    /// <param name="localization">The active localization service.</param>
    public static void Apply(IResourceDictionary resources, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(localization);
        foreach (string key in Keys)
        {
            resources[key] = localization[key];
        }
    }
}
