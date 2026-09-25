using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OgmaLibrary.App.Navigation;

namespace OgmaLibrary.App.Views.Shell;

/// <summary>
/// A single-row toolbar panel with priority-based overflow (Sept-23 Phase 07, T07.4, K11).
/// Children declare <see cref="PriorityProperty"/>: 0 is pinned, larger numbers overflow
/// first. Children that do not fit are arranged at zero size, removed from the tab order and
/// flagged with <see cref="IsOverflowedProperty"/>; the destination toolbar lists them in its
/// "More" menu so no action is ever clipped or unreachable.
/// </summary>
public sealed class PriorityToolbarPanel : Panel
{
    /// <summary>Overflow priority of a child: 0 is pinned, larger numbers overflow first.</summary>
    public static readonly AttachedProperty<int> PriorityProperty =
        AvaloniaProperty.RegisterAttached<PriorityToolbarPanel, Control, int>("Priority", 1);

    /// <summary>Set by the panel on children that currently live in the overflow menu.</summary>
    public static readonly AttachedProperty<bool> IsOverflowedProperty =
        AvaloniaProperty.RegisterAttached<PriorityToolbarPanel, Control, bool>("IsOverflowed");

    /// <summary>Horizontal space between items.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PriorityToolbarPanel, double>(nameof(Spacing), 4);

    private bool[] _visible = [];

    static PriorityToolbarPanel()
    {
        AffectsMeasure<PriorityToolbarPanel>(SpacingProperty);
    }

    /// <summary>Raised after the set of overflowed children changes.</summary>
    public event EventHandler? OverflowChanged;

    /// <summary>Horizontal space between items.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Whether any child is currently overflowed.</summary>
    public bool HasOverflow => Children.Any(child => child.IsVisible && GetIsOverflowed(child));

    /// <summary>The children currently in the overflow menu, in display order.</summary>
    public IEnumerable<Control> OverflowedChildren =>
        Children.Where(child => child.IsVisible && GetIsOverflowed(child));

    /// <summary>Gets the overflow priority of a child.</summary>
    /// <param name="control">The child.</param>
    /// <returns>The priority.</returns>
    public static int GetPriority(Control control) => control.GetValue(PriorityProperty);

    /// <summary>Sets the overflow priority of a child.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">The priority.</param>
    public static void SetPriority(Control control, int value) => control.SetValue(PriorityProperty, value);

    /// <summary>Gets whether a child is overflowed.</summary>
    /// <param name="control">The child.</param>
    /// <returns><see langword="true"/> when the child is in the overflow menu.</returns>
    public static bool GetIsOverflowed(Control control) => control.GetValue(IsOverflowedProperty);

    /// <summary>Sets whether a child is overflowed (panel use only).</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">The flag.</param>
    public static void SetIsOverflowed(Control control, bool value) => control.SetValue(IsOverflowedProperty, value);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var slots = new List<ToolbarSlot>(Children.Count);
        double height = 0;
        foreach (Control child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            double width = child.IsVisible ? child.DesiredSize.Width + Spacing : 0;
            slots.Add(new ToolbarSlot(GetPriority(child), width));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        bool[] visible = ToolbarOverflow.Fit(slots, availableSize.Width + Spacing);
        bool changed = false;
        double used = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            Control child = Children[i];
            bool overflowed = child.IsVisible && !visible[i];
            if (GetIsOverflowed(child) != overflowed)
            {
                SetIsOverflowed(child, overflowed);
                KeyboardNavigation.SetIsTabStop(child, !overflowed);
                KeyboardNavigation.SetTabNavigation(child, overflowed ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
                changed = true;
            }

            if (visible[i])
            {
                used += slots[i].Width;
            }
        }

        _visible = visible;
        if (changed)
        {
            OverflowChanged?.Invoke(this, EventArgs.Empty);
        }

        double total = Math.Max(0, used - Spacing);
        return new Size(double.IsInfinity(availableSize.Width) ? total : Math.Min(total, availableSize.Width), height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            Control child = Children[i];
            if (i < _visible.Length && _visible[i] && child.IsVisible)
            {
                double width = child.DesiredSize.Width;
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width + Spacing;
            }
            else
            {
                child.Arrange(default);
            }
        }

        return finalSize;
    }
}
