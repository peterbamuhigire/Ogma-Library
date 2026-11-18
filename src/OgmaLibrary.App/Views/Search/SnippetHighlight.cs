using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using OgmaLibrary.App.ViewModels.Search;

namespace OgmaLibrary.App.Views.Search;

/// <summary>
/// Renders a search snippet with the matched terms emphasised (Sept-23 Phase 13, task 13.7).
/// Emphasis uses weight only, so it inherits the theme's text colour and works in both themes.
/// </summary>
public static class SnippetHighlight
{
    /// <summary>The result whose snippet and highlight spans the text block shows.</summary>
    public static readonly AttachedProperty<SearchResultItem?> ResultProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, SearchResultItem?>("Result", typeof(SnippetHighlight));

    static SnippetHighlight()
    {
        ResultProperty.Changed.AddClassHandler<TextBlock>(OnResultChanged);
    }

    /// <summary>Gets the attached result.</summary>
    public static SearchResultItem? GetResult(TextBlock element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(ResultProperty);
    }

    /// <summary>Sets the attached result.</summary>
    public static void SetResult(TextBlock element, SearchResultItem? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ResultProperty, value);
    }

    private static void OnResultChanged(TextBlock block, AvaloniaPropertyChangedEventArgs args)
    {
        var inlines = new InlineCollection();
        if (args.NewValue is SearchResultItem { Snippet.Length: > 0 } item)
        {
            string text = item.Snippet;
            int position = 0;
            foreach (var span in (item.SnippetSpans ?? []).OrderBy(span => span.Start))
            {
                if (span.Start < position || span.Start >= text.Length || span.Length <= 0)
                {
                    continue;
                }

                int length = Math.Min(span.Length, text.Length - span.Start);
                if (span.Start > position)
                {
                    inlines.Add(new Run(text[position..span.Start]));
                }

                inlines.Add(new Run(text.Substring(span.Start, length)) { FontWeight = FontWeight.Bold });
                position = span.Start + length;
            }

            if (position < text.Length)
            {
                inlines.Add(new Run(text[position..]));
            }
        }

        block.Inlines = inlines;
    }
}
