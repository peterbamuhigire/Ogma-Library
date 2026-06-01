using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>Generates readable PNG book-spine textures for the 3D bookshelf.</summary>
public sealed class SpineTextureGenerator
{
    /// <summary>Output texture width in pixels.</summary>
    public const int Width = 128;

    /// <summary>Output texture height in pixels.</summary>
    public const int Height = 512;

    private const float Padding = 14;

    /// <summary>Generates a 128x512 PNG spine texture.</summary>
    public static byte[] GeneratePng(SpineTextureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(request.BackgroundColor);

        SKColor textColor = ChooseTextColor(request.BackgroundColor);
        using var paint = new SKPaint
        {
            Color = textColor,
            IsAntialias = true,
        };
        using var titleFont = new SKFont(SKTypeface.Default, 24) { Embolden = true };
        using var authorFont = new SKFont(SKTypeface.Default, 15);
        using var rulePaint = new SKPaint
        {
            Color = textColor.WithAlpha(90),
            IsAntialias = true,
            StrokeWidth = 2,
        };

        canvas.DrawRoundRect(new SKRect(8, 8, Width - 8, Height - 8), 10, 10, rulePaint);
        canvas.DrawLine(Padding, 72, Width - Padding, 72, rulePaint);

        string[] titleLines = WrapTitle(request.Title, titleFont, Width - (Padding * 2), maxLines: 2);
        float y = 128;
        foreach (string line in titleLines)
        {
            canvas.DrawText(line, Padding, y, SKTextAlign.Left, titleFont, paint);
            y += 32;
        }

        string author = Ellipsize(request.Author, authorFont, Width - (Padding * 2));
        canvas.DrawText(author, Padding, Height - 44, SKTextAlign.Left, authorFont, paint);

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }

    /// <summary>Chooses dark or light text for readable contrast against a spine background.</summary>
    public static SKColor ChooseTextColor(SKColor background)
    {
        double luminance = ((0.2126 * background.Red) + (0.7152 * background.Green) + (0.0722 * background.Blue)) / 255.0;
        return luminance > 0.52 ? new SKColor(30, 30, 30) : SKColors.White;
    }

    private static string[] WrapTitle(string title, SKFont font, float maxWidth, int maxLines)
    {
        string normalized = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> lines = [];
        string current = string.Empty;

        foreach (string word in words)
        {
            string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (font.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            current = word;
            if (lines.Count == maxLines)
            {
                break;
            }
        }

        if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        if (lines.Count == 0)
        {
            lines.Add(Ellipsize(normalized, font, maxWidth));
        }

        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
        }

        if (words.Length > string.Join(' ', lines).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
        {
            int last = lines.Count - 1;
            lines[last] = Ellipsize(lines[last], font, maxWidth);
        }

        return lines.ToArray();
    }

    private static string Ellipsize(string value, SKFont font, float maxWidth)
    {
        const string Ellipsis = "...";
        string text = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        if (font.MeasureText(text) <= maxWidth)
        {
            return text;
        }

        while (text.Length > 0 && font.MeasureText(text + Ellipsis) > maxWidth)
        {
            text = text[..^1];
        }

        return string.IsNullOrEmpty(text) ? Ellipsis : text + Ellipsis;
    }
}

/// <summary>Input data for a generated 3D shelf spine texture.</summary>
public sealed record SpineTextureRequest(
    string BookId,
    string Title,
    string Author,
    SKColor BackgroundColor);
