using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// A virtualising items panel that lays out fixed-size cells in rows, left to right
/// (Sept-23 Phase 10, task 10.2). Only the rows in the effective viewport, plus one row of
/// buffer on each side, have containers; containers leaving the range are recycled. The keyboard
/// focused container is kept realised so focus is never lost while scrolling.
/// </summary>
/// <remarks>
/// Cell size comes from the card-size tokens (<c>Size.Catalogue.Slot.*</c>). Arrow keys move by
/// one cell or one row, Home/End go to the first and last item and PageUp/PageDown move by a
/// viewport of rows; targets outside the realised range are realised and scrolled into view.
/// </remarks>
public sealed class VirtualizingUniformGridPanel : VirtualizingPanel
{
    /// <summary>Defines the <see cref="ItemWidth"/> property.</summary>
    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<VirtualizingUniformGridPanel, double>(nameof(ItemWidth), 176);

    /// <summary>Defines the <see cref="ItemHeight"/> property.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<VirtualizingUniformGridPanel, double>(nameof(ItemHeight), 312);

    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<VirtualizingUniformGridPanel, Control, object?>("RecycleKey");

    private static readonly object OwnContainerKey = new();

    private readonly Dictionary<int, Control> _realized = [];
    private readonly Dictionary<object, Stack<Control>> _pool = [];
    private readonly List<(Control Container, int Index)> _released = [];
    private Rect _viewport;
    private int _columns = 1;
    private int _scrollToIndex = -1;

    static VirtualizingUniformGridPanel()
    {
        AffectsMeasure<VirtualizingUniformGridPanel>(ItemWidthProperty, ItemHeightProperty);
    }

    /// <summary>Initializes the panel.</summary>
    public VirtualizingUniformGridPanel()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    /// <summary>The width of one cell, including the card's outer margin.</summary>
    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>The height of one cell, including the card's outer margin.</summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>The number of columns in the last measure.</summary>
    public int Columns => _columns;

    /// <summary>The number of containers currently bound to items.</summary>
    public int RealizedCount => _realized.Count;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        IReadOnlyList<object?> items = Items;
        double cellWidth = Math.Max(1, ItemWidth);
        double cellHeight = Math.Max(1, ItemHeight);
        double width = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : Math.Max(cellWidth, _viewport.Width);
        _columns = Math.Max(1, (int)Math.Floor(width / cellWidth));

        int count = items.Count;
        int rows = count == 0 ? 0 : ((count - 1) / _columns) + 1;
        (int first, int last) = VisibleRange(count, cellHeight, availableSize.Height);

        foreach (KeyValuePair<int, Control> entry in _realized.ToList())
        {
            if ((entry.Key < first || entry.Key > last) &&
                entry.Key != _scrollToIndex &&
                !entry.Value.IsKeyboardFocusWithin)
            {
                Recycle(entry.Key, entry.Value);
            }
        }

        for (int index = Math.Max(0, first); index <= last; index++)
        {
            if (!_realized.ContainsKey(index))
            {
                Realize(index, items[index]);
            }
        }

        ClearReleased();
        var cell = new Size(cellWidth, cellHeight);
        foreach (Control container in _realized.Values)
        {
            container.Measure(cell);
        }

        return new Size(double.IsFinite(availableSize.Width) ? width : _columns * cellWidth, rows * cellHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (KeyValuePair<int, Control> entry in _realized)
        {
            entry.Value.Arrange(SlotRect(entry.Key));
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override Control? ScrollIntoView(int index)
    {
        IReadOnlyList<object?> items = Items;
        if (index < 0 || index >= items.Count)
        {
            return null;
        }

        if (_realized.TryGetValue(index, out Control? existing))
        {
            existing.BringIntoView();
            return existing;
        }

        if (!this.IsAttachedToVisualTree())
        {
            return null;
        }

        // Realise the target in place, scroll to it, then let one layout pass recycle around it.
        _scrollToIndex = index;
        try
        {
            Control container = Realize(index, items[index]);
            container.Measure(new Size(ItemWidth, ItemHeight));
            container.Arrange(SlotRect(index));
            container.BringIntoView();
            UpdateLayout();
            return container;
        }
        finally
        {
            _scrollToIndex = -1;
        }
    }

    /// <inheritdoc />
    protected override Control? ContainerFromIndex(int index) =>
        _realized.TryGetValue(index, out Control? container) ? container : null;

    /// <inheritdoc />
    protected override int IndexFromContainer(Control container)
    {
        foreach (KeyValuePair<int, Control> entry in _realized)
        {
            if (ReferenceEquals(entry.Value, container))
            {
                return entry.Key;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    protected override IEnumerable<Control>? GetRealizedContainers() => _realized.Values;

    /// <inheritdoc />
    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        int count = Items.Count;
        if (count == 0)
        {
            return null;
        }

        int current = IndexOf(from);
        int target = NavigationTarget(direction, current, count);
        return target < 0 ? null : ScrollIntoView(target);
    }

    /// <inheritdoc />
    protected override void OnItemsControlChanged(ItemsControl? oldValue)
    {
        base.OnItemsControlChanged(oldValue);
        oldValue?.RemoveHandler(KeyDownEvent, OnOwnerKeyDown);
        ItemsControl?.AddHandler(KeyDownEvent, OnOwnerKeyDown, RoutingStrategies.Tunnel);
        RecycleAll();
        _released.Clear();
        _pool.Clear();
        if (Children.Count > 0)
        {
            RemoveInternalChildRange(0, Children.Count);
        }

        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnItemsChanged(items, e);

        // Indexes before the change keep their containers (no flicker, stable automation
        // elements); everything from the change onwards is re-bound on the next measure.
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Replace:
                RecycleRange(e.OldStartingIndex, e.OldStartingIndex + (e.OldItems?.Count ?? 1) - 1);
                break;
            case NotifyCollectionChangedAction.Add:
                RecycleRange(e.NewStartingIndex, int.MaxValue);
                break;
            case NotifyCollectionChangedAction.Remove:
                RecycleRange(e.OldStartingIndex, int.MaxValue);
                break;
            default:
                RecycleAll();
                break;
        }

        InvalidateMeasure();
    }

    /// <summary>The target index for a navigation key, or -1 when focus should not move.</summary>
    /// <param name="direction">The navigation direction.</param>
    /// <param name="current">The current index, or -1 when nothing is focused.</param>
    /// <param name="count">The item count.</param>
    /// <returns>The target index, or -1.</returns>
    internal int NavigationTarget(NavigationDirection direction, int current, int count)
    {
        if (current < 0)
        {
            return direction == NavigationDirection.Last ? count - 1 : 0;
        }

        int rowsPerPage = Math.Max(1, (int)Math.Floor(_viewport.Height / Math.Max(1, ItemHeight)));
        int pageItems = rowsPerPage * _columns;
        int lastRow = (count - 1) / _columns;
        int target = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Left or NavigationDirection.Previous => current - 1,
            NavigationDirection.Right or NavigationDirection.Next => current + 1,
            NavigationDirection.Up => current - _columns,
            NavigationDirection.Down when current / _columns < lastRow => Math.Min(count - 1, current + _columns),
            NavigationDirection.Down => -1,
            NavigationDirection.PageUp => Math.Max(0, current - pageItems),
            NavigationDirection.PageDown => Math.Min(count - 1, current + pageItems),
            _ => -1,
        };
        return target >= 0 && target < count && target != current ? target : -1;
    }

    private void OnOwnerKeyDown(object? sender, KeyEventArgs e)
    {
        // The ScrollViewer inside the ListBox would otherwise consume PageUp/PageDown and scroll
        // without moving focus; tunnel first so the keys move focus by a page of rows.
        NavigationDirection? direction = e.Key switch
        {
            Key.PageUp => NavigationDirection.PageUp,
            Key.PageDown => NavigationDirection.PageDown,
            _ => null,
        };
        if (direction is null || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        int current = IndexOf(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement());
        if (current < 0)
        {
            return;
        }

        int target = NavigationTarget(direction.Value, current, Items.Count);
        if (target >= 0 && ScrollIntoView(target) is { } container)
        {
            container.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    private (int First, int Last) VisibleRange(int count, double cellHeight, double availableHeight)
    {
        if (count == 0)
        {
            return (0, -1);
        }

        Rect viewport = _viewport;
        if (viewport.Height <= 0)
        {
            // Before the first viewport report, realise one screen from the top.
            double height = double.IsFinite(availableHeight) ? availableHeight : cellHeight * 4;
            viewport = new Rect(0, 0, 0, height);
        }

        int firstRow = Math.Max(0, (int)Math.Floor((viewport.Top - cellHeight) / cellHeight));
        int lastRow = (int)Math.Floor((viewport.Bottom + cellHeight) / cellHeight);
        int first = firstRow * _columns;
        int last = Math.Min(count - 1, ((lastRow + 1) * _columns) - 1);
        return (first, last);
    }

    private Rect SlotRect(int index)
    {
        int row = index / _columns;
        int column = index % _columns;
        return new Rect(column * ItemWidth, row * ItemHeight, ItemWidth, ItemHeight);
    }

    private int IndexOf(IInputElement? from)
    {
        for (Visual? visual = from as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control control && ReferenceEquals(control.GetVisualParent(), this))
            {
                return IndexFromContainer(control);
            }
        }

        return -1;
    }

    private Control Realize(int index, object? item)
    {
        var generator = ItemContainerGenerator!;
        Control container;
        if (generator.NeedsContainer(item, index, out object? recycleKey))
        {
            object key = recycleKey ?? OwnContainerKey;
            int released = FindReleased(key, item, index);
            if (released >= 0)
            {
                // Released in this layout pass and still bound: replacing the content lets the
                // recycling data template reuse the card's visual tree instead of rebuilding it,
                // which is most of the cost of a scroll step.
                container = _released[released].Container;
                _released.RemoveAt(released);
                if (container is ContentControl content)
                {
                    content.SetCurrentValue(ContentControl.ContentProperty, item);
                    content.SetCurrentValue(DataContextProperty, item);
                }

                container.SetCurrentValue(IsVisibleProperty, true);
            }
            else if (_pool.TryGetValue(key, out Stack<Control>? stack) && stack.Count > 0)
            {
                container = stack.Pop();
                container.SetCurrentValue(IsVisibleProperty, true);
            }
            else
            {
                container = generator.CreateContainer(item, index, recycleKey);
                container.SetValue(RecycleKeyProperty, key);
                AddInternalChild(container);
            }

            generator.PrepareItemContainer(container, item, index);
            _realized[index] = container;
            generator.ItemContainerPrepared(container, item, index);
        }
        else
        {
            container = (Control)item!;
            container.SetValue(RecycleKeyProperty, OwnContainerKey);
            generator.PrepareItemContainer(container, container, index);
            AddInternalChild(container);
            _realized[index] = container;
            generator.ItemContainerPrepared(container, container, index);
        }

        return container;
    }

    private void Recycle(int index, Control container)
    {
        _realized.Remove(index);
        object? key = container.GetValue(RecycleKeyProperty);
        if (key is null || ReferenceEquals(key, OwnContainerKey))
        {
            ItemContainerGenerator?.ClearItemContainer(container);
            RemoveInternalChild(container);
            return;
        }

        // Hidden but still bound until the end of this measure, so a row scrolling in can take it
        // over cheaply; whatever is left is cleared then (no stale item or accessible name).
        container.SetCurrentValue(IsVisibleProperty, false);
        _released.Add((container, index));
    }

    /// <summary>
    /// A released container for <paramref name="item"/>: preferably the one that already showed the
    /// same item, then the one at the same index (a refreshed record), then any. Keeping a book on
    /// its container across refreshes keeps UI Automation elements pointing at the same book.
    /// </summary>
    private int FindReleased(object key, object? item, int index)
    {
        int sameIndex = -1;
        int any = -1;
        for (int i = _released.Count - 1; i >= 0; i--)
        {
            (Control container, int previous) = _released[i];
            if (!Equals(container.GetValue(RecycleKeyProperty), key))
            {
                continue;
            }

            if (Equals(container.DataContext, item))
            {
                return i;
            }

            if (previous == index && sameIndex < 0)
            {
                sameIndex = i;
            }

            if (any < 0)
            {
                any = i;
            }
        }

        return sameIndex >= 0 ? sameIndex : any;
    }

    private void ClearReleased()
    {
        foreach ((Control container, _) in _released)
        {
            ItemContainerGenerator?.ClearItemContainer(container);

            // The generator prepares only unset values, so a re-bound container must not keep the
            // previous book as its data context (its accessible name would announce that book).
            container.ClearValue(DataContextProperty);
            container.ClearValue(ContentControl.ContentProperty);
            object key = container.GetValue(RecycleKeyProperty)!;
            if (!_pool.TryGetValue(key, out Stack<Control>? stack))
            {
                stack = new Stack<Control>();
                _pool[key] = stack;
            }

            stack.Push(container);
        }

        _released.Clear();
    }

    private void RecycleRange(int first, int last)
    {
        foreach (KeyValuePair<int, Control> entry in _realized.ToList())
        {
            if (entry.Key >= first && entry.Key <= last)
            {
                Recycle(entry.Key, entry.Value);
            }
        }
    }

    private void RecycleAll() => RecycleRange(int.MinValue, int.MaxValue);

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        Rect viewport = e.EffectiveViewport;
        if (viewport == _viewport)
        {
            return;
        }

        bool rangeMayChange =
            Math.Abs(viewport.Top - _viewport.Top) >= 1 ||
            Math.Abs(viewport.Height - _viewport.Height) >= 1 ||
            Math.Abs(viewport.Width - _viewport.Width) >= 1;
        _viewport = viewport;
        if (rangeMayChange)
        {
            InvalidateMeasure();
        }
    }
}
