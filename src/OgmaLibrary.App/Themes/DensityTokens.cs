using Avalonia.Controls;

namespace OgmaLibrary.App.Themes;

/// <summary>
/// Applies the user's density preference to the size tokens. Baselines are read from the merged
/// token dictionaries (<c>Themes/Tokens.axaml</c>), which are never written, so the Lamplit type
/// scale (D-14) has one source and scaling is repeatable in either direction.
/// </summary>
public static class DensityTokens
{
    /// <summary>The smallest font size the scale allows, at any density.</summary>
    public const double FontFloor = 12;

    private static readonly string[] Keys =
    [
        "Type.Size.Caption", "Type.Size.Small", "Type.Size.Body", "Type.Size.BodyLarge",
        "Type.Size.BodyMedium", "Type.Size.Subtitle", "Type.Size.Title", "Type.Size.Headline",
        "Type.Size.Display", "Type.Size.Glyph",
        "Type.LineHeight.Caption", "Type.LineHeight.Small", "Type.LineHeight.Body",
        "Type.LineHeight.BodyLarge", "Type.LineHeight.Subtitle", "Type.LineHeight.Title",
        "Type.LineHeight.Headline", "Type.LineHeight.Display",
        "Spacing.XXS", "Spacing.XS", "Spacing.S", "Spacing.M", "Spacing.L", "Spacing.XL",
    ];

    /// <summary>
    /// Writes each size token, scaled from its merged baseline, into the top level of
    /// <paramref name="resources"/>. Font sizes never drop below <see cref="FontFloor"/>.
    /// </summary>
    /// <param name="resources">The application resource dictionary.</param>
    /// <param name="scale">1.0 for comfortable density; below 1.0 for compact.</param>
    public static void Apply(IResourceDictionary resources, double scale)
    {
        ArgumentNullException.ThrowIfNull(resources);
        foreach (string key in Keys)
        {
            if (!TryGetBaseline(resources, key, out double baseline))
            {
                continue;
            }

            double value = baseline * scale;
            if (key.StartsWith("Type.Size.", StringComparison.Ordinal))
            {
                value = Math.Max(FontFloor, value);
            }

            resources[key] = value;
        }
    }

    private static bool TryGetBaseline(IResourceDictionary resources, string key, out double baseline)
    {
        foreach (IResourceProvider provider in resources.MergedDictionaries)
        {
            if (provider.TryGetResource(key, null, out object? raw) && raw is double value)
            {
                baseline = value;
                return true;
            }
        }

        baseline = 0;
        return false;
    }
}
