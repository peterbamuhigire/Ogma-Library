namespace OgmaLibrary.App.Navigation;

/// <summary>A toolbar item as seen by the overflow algorithm.</summary>
/// <param name="Priority">0 is pinned (never overflows); larger numbers overflow first.</param>
/// <param name="Width">The desired width including spacing.</param>
public readonly record struct ToolbarSlot(int Priority, double Width);

/// <summary>
/// Priority-based toolbar overflow (Sept-23 Phase 07, T07.4): pinned items always stay; the
/// remaining items are kept from the lowest priority number upwards while they fit, and the
/// rest move to the "More" menu. Items of equal priority overflow together so a group never
/// splits.
/// </summary>
public static class ToolbarOverflow
{
    /// <summary>Returns, for each slot, whether it stays visible in <paramref name="available"/> width.</summary>
    /// <param name="slots">The toolbar items in display order.</param>
    /// <param name="available">The available width.</param>
    /// <returns>A visibility flag per slot, in the same order.</returns>
    public static bool[] Fit(IReadOnlyList<ToolbarSlot> slots, double available)
    {
        ArgumentNullException.ThrowIfNull(slots);
        bool[] visible = new bool[slots.Count];
        double used = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Priority <= 0)
            {
                visible[i] = true;
                used += slots[i].Width;
            }
        }

        foreach (int priority in slots.Where(slot => slot.Priority > 0).Select(slot => slot.Priority).Distinct().Order())
        {
            double groupWidth = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Priority == priority)
                {
                    groupWidth += slots[i].Width;
                }
            }

            if (used + groupWidth > available)
            {
                break;
            }

            used += groupWidth;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Priority == priority)
                {
                    visible[i] = true;
                }
            }
        }

        return visible;
    }
}
