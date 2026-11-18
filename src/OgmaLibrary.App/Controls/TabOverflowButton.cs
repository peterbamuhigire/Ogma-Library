using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace OgmaLibrary.App.Controls;

/// <summary>
/// The trailing "More tabs" button of the compact tab strip (Sept-23 Phase 09, criterion 6).
/// It is visible only while the one-row header strip is wider than its viewport, and opens a
/// menu of the tabs that are not fully in view; choosing one selects it and scrolls it into
/// view. A style cannot measure overflow, so this small helper does it for the template.
/// </summary>
public sealed class TabOverflowButton : Button
{
    /// <summary>Defines the <see cref="Tabs"/> property.</summary>
    public static readonly StyledProperty<TabControl?> TabsProperty =
        AvaloniaProperty.Register<TabOverflowButton, TabControl?>(nameof(Tabs));

    /// <summary>Defines the <see cref="Scroller"/> property.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollerProperty =
        AvaloniaProperty.Register<TabOverflowButton, ScrollViewer?>(nameof(Scroller));

    private const double Tolerance = 0.5;

    /// <summary>Initializes a new instance of the <see cref="TabOverflowButton"/> class.</summary>
    public TabOverflowButton()
    {
        IsVisible = false;
    }

    /// <summary>Gets or sets the tab control whose headers this button summarises.</summary>
    public TabControl? Tabs
    {
        get => GetValue(TabsProperty);
        set => SetValue(TabsProperty, value);
    }

    /// <summary>Gets or sets the horizontal scroll viewer that hosts the tab headers.</summary>
    public ScrollViewer? Scroller
    {
        get => GetValue(ScrollerProperty);
        set => SetValue(ScrollerProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>Returns the indices of tabs whose headers are not fully inside the viewport.</summary>
    /// <returns>Ascending tab indices; empty when every header fits.</returns>
    public IReadOnlyList<int> HiddenTabIndices()
    {
        if (Tabs is not { } tabs || Scroller is not { } scroller)
        {
            return [];
        }

        var hidden = new List<int>();
        double viewport = scroller.Viewport.Width;
        for (int i = 0; i < tabs.ItemCount; i++)
        {
            if (tabs.ContainerFromIndex(i) is not Control container ||
                container.TranslatePoint(default, scroller) is not { } origin)
            {
                hidden.Add(i);
                continue;
            }

            double left = origin.X;
            double right = left + container.Bounds.Width;
            if (left < -Tolerance || right > viewport + Tolerance)
            {
                hidden.Add(i);
            }
        }

        return hidden;
    }

    /// <summary>Builds one menu item per hidden tab; clicking an item selects that tab.</summary>
    /// <returns>The menu items, in tab order.</returns>
    public IReadOnlyList<MenuItem> BuildMenuItems()
    {
        var items = new List<MenuItem>();
        foreach (int index in HiddenTabIndices())
        {
            int target = index;
            var item = new MenuItem { Header = HeaderText(target) };
            item.Click += (_, _) => Select(target);
            items.Add(item);
        }

        return items;
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        base.OnClick();
        IReadOnlyList<MenuItem> items = BuildMenuItems();
        if (items.Count == 0)
        {
            return;
        }

        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        foreach (MenuItem item in items)
        {
            flyout.Items.Add(item);
        }

        flyout.ShowAt(this);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Tabs ??= TemplatedParent as TabControl;
        UpdateVisibility();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScrollerProperty)
        {
            if (change.OldValue is ScrollViewer oldScroller)
            {
                oldScroller.ScrollChanged -= OnScrollChanged;
            }

            if (change.NewValue is ScrollViewer newScroller)
            {
                newScroller.ScrollChanged += OnScrollChanged;
            }

            UpdateVisibility();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateVisibility();

    private void UpdateVisibility()
    {
        IsVisible = Scroller is { } scroller &&
            scroller.Extent.Width - scroller.Viewport.Width > Tolerance;
    }

    private void Select(int index)
    {
        if (Tabs is not { } tabs)
        {
            return;
        }

        tabs.SelectedIndex = index;
        if (tabs.ContainerFromIndex(index) is Control container)
        {
            container.BringIntoView();
            container.Focus();
        }
    }

    private string HeaderText(int index)
    {
        object? header = Tabs?.ContainerFromIndex(index) is HeaderedContentControl container
            ? container.Header
            : Tabs?.Items[index];
        return header switch
        {
            string text => text,
            Control control when AutomationProperties.GetName(control) is { Length: > 0 } name => name,
            HeaderedContentControl headered => headered.Header?.ToString() ?? string.Empty,
            _ => header?.ToString() ?? string.Empty,
        };
    }
}
