using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// The deterministic design of a generated cloth-bound cover (Sept-23 Phase 09, K15): the jewel
/// cloth colour is chosen from a stable hash of the title, so the same title always yields the
/// same cover and a library of untitled-looking placeholders becomes recognisable at a glance.
/// </summary>
/// <param name="ClothKey">The <c>Color.Accent.*</c> token key used for the cloth.</param>
/// <param name="PatternIndex">Which of the three cloth patterns to draw (0–2).</param>
public sealed record CoverDesign(string ClothKey, int PatternIndex)
{
    /// <summary>The cloth colours, in hash order. All carry the gilt title at >= 4.5:1.</summary>
    public static IReadOnlyList<string> ClothKeys { get; } =
    [
        "Color.Accent.Oak",
        "Color.Accent.Ink",
        "Color.Accent.Sage",
        "Color.Accent.Clay",
        "Color.Accent.Plum",
        "Color.Accent.Slate",
        "Color.Accent.Saffron",
        "Color.Accent.Teal",
    ];

    /// <summary>Returns the design for a title; stable across runs, machines and cultures.</summary>
    /// <param name="title">The book title (null or blank uses a fixed design).</param>
    /// <returns>The cover design.</returns>
    public static CoverDesign For(string? title)
    {
        uint hash = StableHash(title?.Trim() ?? string.Empty);
        return new CoverDesign(ClothKeys[(int)(hash % (uint)ClothKeys.Count)], (int)(hash / 8 % 3));
    }

    /// <summary>FNV-1a over the case-folded UTF-16 code units (string.GetHashCode is randomised).</summary>
    /// <param name="value">The text to hash.</param>
    /// <returns>The 32-bit hash.</returns>
    public static uint StableHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        uint hash = 2166136261;
        foreach (char c in value.ToUpperInvariant())
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash;
    }
}

/// <summary>
/// Draws a generated cloth-bound cover: a jewel cloth with a soft light-to-dark gradient, a
/// woven pattern, a darker spine band with an edge highlight, a gilt frame, the title set in
/// Spectral and the author in small capitals. Pure vector drawing; nothing is written to disk.
/// </summary>
public sealed class GeneratedCover : Control
{
    /// <summary>The title shown on the cover.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<GeneratedCover, string?>(nameof(Title));

    /// <summary>The author shown under the title (optional).</summary>
    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<GeneratedCover, string?>(nameof(Author));

    static GeneratedCover()
    {
        AffectsRender<GeneratedCover>(TitleProperty, AuthorProperty);
        ClipToBoundsProperty.OverrideDefaultValue<GeneratedCover>(true);
    }

    /// <summary>The title shown on the cover.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The author shown under the title.</summary>
    public string? Author
    {
        get => GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }

    /// <summary>The design currently drawn (for tests and accessibility tooling).</summary>
    public CoverDesign Design => CoverDesign.For(Title);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Rect bounds = new(Bounds.Size);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        CoverDesign design = Design;
        Color cloth = ResolveColor(design.ClothKey, Color.FromRgb(0x4E, 0x60, 0x72));
        Color gilt = ResolveColor("Color.Cover.Gilt", Colors.White);

        // Cloth: light at the top, deeper at the foot.
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.3, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Shade(cloth, 1.12), 0),
                new GradientStop(cloth, 0.45),
                new GradientStop(Shade(cloth, 0.78), 1),
            },
        };
        context.DrawRectangle(gradient, null, bounds);
        DrawWeave(context, bounds, design.PatternIndex, Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));

        // Spine band with an edge highlight.
        double spine = Math.Max(6, bounds.Width * 0.07);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x4D, 0, 0, 0)), null, new Rect(0, 0, spine, bounds.Height));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)), null, new Rect(spine, 0, 1, bounds.Height));

        // Gilt frame.
        double inset = Math.Max(6, bounds.Width * 0.08);
        var frame = new Rect(spine + inset, inset, bounds.Width - spine - (2 * inset), bounds.Height - (2 * inset));
        if (frame.Width > 20 && frame.Height > 20)
        {
            context.DrawRectangle(null, new Pen(new SolidColorBrush(gilt, 0.7), 1), frame);
        }

        if (bounds.Width < 100)
        {
            DrawMonogram(context, bounds, spine, gilt);
            return;
        }

        double textWidth = Math.Max(10, frame.Width - 12);
        FontFamily display = ResolveFont("Type.FontFamily.Display");
        FontFamily body = ResolveFont("Type.FontFamily.Body");
        double titleSize = Math.Clamp(bounds.Width / 9.5, 12, 26);
        var title = new FormattedText(
            string.IsNullOrWhiteSpace(Title) ? "—" : Title.Trim(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(display, FontStyle.Normal, FontWeight.SemiBold),
            titleSize,
            new SolidColorBrush(gilt))
        {
            MaxTextWidth = textWidth,
            MaxTextHeight = frame.Height * 0.62,
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.WordEllipsis,
        };
        double centreX = frame.X + ((frame.Width - textWidth) / 2);
        double titleTop = frame.Y + (frame.Height * 0.18);
        context.DrawText(title, new Point(centreX, titleTop));

        // A short gilt rule between title and author.
        double ruleY = titleTop + title.Height + (titleSize * 0.5);
        double ruleHalf = Math.Min(18, frame.Width / 6);
        double midX = frame.X + (frame.Width / 2);
        if (ruleY < frame.Bottom - 8)
        {
            context.DrawLine(new Pen(new SolidColorBrush(gilt, 0.8), 1), new Point(midX - ruleHalf, ruleY), new Point(midX + ruleHalf, ruleY));
        }

        if (!string.IsNullOrWhiteSpace(Author))
        {
            double authorSize = Math.Clamp(titleSize * 0.56, 12, 14);
            var author = new FormattedText(
                Author.Trim().ToUpper(System.Globalization.CultureInfo.CurrentCulture),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(body, FontStyle.Normal, FontWeight.Medium),
                authorSize,
                new SolidColorBrush(gilt))
            {
                MaxTextWidth = textWidth,
                MaxLineCount = 1,
                TextAlignment = TextAlignment.Center,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            double authorTop = Math.Min(ruleY + (authorSize * 0.6), frame.Bottom - author.Height - 4);
            if (authorTop > titleTop + title.Height)
            {
                context.DrawText(author, new Point(centreX, authorTop));
            }
        }
    }

    /// <summary>Scales a colour's channels (>1 lightens toward white, &lt;1 darkens).</summary>
    /// <param name="color">The source colour.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>The shaded colour.</returns>
    internal static Color Shade(Color color, double factor)
    {
        static byte Channel(byte c, double f) => f >= 1
            ? (byte)Math.Clamp(c + ((255 - c) * (f - 1)), 0, 255)
            : (byte)Math.Clamp(c * f, 0, 255);

        return Color.FromArgb(color.A, Channel(color.R, factor), Channel(color.G, factor), Channel(color.B, factor));
    }

    /// <summary>The monogram drawn on thumbnail covers: initials of the first two significant words.</summary>
    /// <param name="title">The title.</param>
    /// <returns>One or two upper-case letters.</returns>
    public static string Monogram(string? title)
    {
        string[] stop = ["THE", "A", "AN", "LE", "LA", "LES", "UN", "UNE", "OF", "DE"];
        string[] words = (title ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .ToArray();
        string[] significant = words.Where(w => !stop.Contains(w.ToUpperInvariant())).ToArray();
        string[] chosen = significant.Length > 0 ? significant : words;
        return chosen.Length == 0
            ? "?"
            : string.Concat(chosen.Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }

    private void DrawMonogram(DrawingContext context, Rect bounds, double spine, Color gilt)
    {
        var text = new FormattedText(
            Monogram(Title),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(ResolveFont("Type.FontFamily.Display"), FontStyle.Normal, FontWeight.SemiBold),
            Math.Clamp(bounds.Width * 0.36, 12, 34),
            new SolidColorBrush(gilt));
        double x = spine + ((bounds.Width - spine - text.Width) / 2);
        double y = (bounds.Height - text.Height) * 0.4;
        context.DrawText(text, new Point(x, y));
        double ruleY = y + text.Height + 2;
        double mid = spine + ((bounds.Width - spine) / 2);
        context.DrawLine(new Pen(new SolidColorBrush(gilt, 0.8), 1), new Point(mid - 8, ruleY), new Point(mid + 8, ruleY));
    }

    private static void DrawWeave(DrawingContext context, Rect bounds, int pattern, Color ink)
    {
        var pen = new Pen(new SolidColorBrush(ink), 1);
        switch (pattern)
        {
            case 0:
                // Fine diagonal twill.
                for (double x = -bounds.Height; x < bounds.Width; x += 6)
                {
                    context.DrawLine(pen, new Point(x, bounds.Height), new Point(x + bounds.Height, 0));
                }

                break;
            case 1:
                // Horizontal linen ribs.
                for (double y = 3; y < bounds.Height; y += 5)
                {
                    context.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
                }

                break;
            default:
                // Dotted buckram.
                var dot = new SolidColorBrush(ink);
                for (double y = 4; y < bounds.Height; y += 8)
                {
                    for (double x = (y % 16 == 4) ? 4 : 8; x < bounds.Width; x += 8)
                    {
                        context.DrawRectangle(dot, null, new Rect(x, y, 1.5, 1.5));
                    }
                }

                break;
        }
    }

    private Color ResolveColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is Color color
            ? color
            : fallback;

    private FontFamily ResolveFont(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is FontFamily font
            ? font
            : FontFamily.Default;
}
