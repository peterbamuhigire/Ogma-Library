using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>
/// Pure mapping from a page index to the pages drawn together with it (FR-READ-004).
/// Single and continuous layouts show one page per slot; a spread pairs neighbours, and with
/// the cover-alone option page 0 is shown by itself before the pairing starts.
/// </summary>
public static class ReaderLayoutPlanner
{
    /// <summary>Returns the zero-based index of the first page of the slot containing <paramref name="pageIndex"/>.</summary>
    /// <param name="mode">The layout.</param>
    /// <param name="pageIndex">A zero-based page index.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="coverAlone">Whether the cover is drawn alone in a spread.</param>
    public static int SlotStart(DisplayMode mode, int pageIndex, int pageCount, bool coverAlone)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        int page = Math.Clamp(pageIndex, 0, pageCount - 1);
        if (mode != DisplayMode.TwoPage)
        {
            return page;
        }

        if (coverAlone)
        {
            return page == 0 ? 0 : page - ((page - 1) % 2);
        }

        return page - (page % 2);
    }

    /// <summary>Returns the pages drawn together with <paramref name="pageIndex"/>, in reading order.</summary>
    /// <param name="mode">The layout.</param>
    /// <param name="pageIndex">A zero-based page index.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="coverAlone">Whether the cover is drawn alone in a spread.</param>
    public static IReadOnlyList<int> PagesInSlot(DisplayMode mode, int pageIndex, int pageCount, bool coverAlone)
    {
        if (pageCount <= 0)
        {
            return [];
        }

        int start = SlotStart(mode, pageIndex, pageCount, coverAlone);
        if (mode != DisplayMode.TwoPage || (coverAlone && start == 0) || start + 1 >= pageCount)
        {
            return [start];
        }

        return [start, start + 1];
    }

    /// <summary>Returns the first page of the next slot, or <c>null</c> after the last slot.</summary>
    /// <param name="mode">The layout.</param>
    /// <param name="pageIndex">A zero-based page index.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="coverAlone">Whether the cover is drawn alone in a spread.</param>
    public static int? NextSlotStart(DisplayMode mode, int pageIndex, int pageCount, bool coverAlone)
    {
        IReadOnlyList<int> slot = PagesInSlot(mode, pageIndex, pageCount, coverAlone);
        if (slot.Count == 0)
        {
            return null;
        }

        int next = slot[^1] + 1;
        return next < pageCount ? next : null;
    }

    /// <summary>Returns the first page of the previous slot, or <c>null</c> before the first slot.</summary>
    /// <param name="mode">The layout.</param>
    /// <param name="pageIndex">A zero-based page index.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="coverAlone">Whether the cover is drawn alone in a spread.</param>
    public static int? PreviousSlotStart(DisplayMode mode, int pageIndex, int pageCount, bool coverAlone)
    {
        int start = SlotStart(mode, pageIndex, pageCount, coverAlone);
        return start <= 0 ? null : SlotStart(mode, start - 1, pageCount, coverAlone);
    }

    /// <summary>Returns the number of slots in the document for the layout.</summary>
    /// <param name="mode">The layout.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="coverAlone">Whether the cover is drawn alone in a spread.</param>
    public static int SlotCount(DisplayMode mode, int pageCount, bool coverAlone)
    {
        if (pageCount <= 0)
        {
            return 0;
        }

        if (mode != DisplayMode.TwoPage)
        {
            return pageCount;
        }

        return coverAlone ? 1 + (pageCount / 2) : (pageCount + 1) / 2;
    }

    /// <summary>
    /// Returns the inclusive range of page indexes to keep rendered for a continuous viewport:
    /// every page the viewport touches plus <paramref name="margin"/> pages each side.
    /// </summary>
    /// <param name="offset">The vertical scroll offset in device-independent pixels.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    /// <param name="pageStride">The page height plus the gap between pages.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="margin">Extra pages realised above and below the visible ones.</param>
    public static (int First, int Last) RealisedRange(
        double offset, double viewportHeight, double pageStride, int pageCount, int margin)
    {
        if (pageCount <= 0 || pageStride <= 0)
        {
            return (0, -1);
        }

        double top = Math.Max(0, offset);
        int firstVisible = (int)Math.Floor(top / pageStride);
        int lastVisible = (int)Math.Floor((top + Math.Max(0, viewportHeight) - 0.001) / pageStride);
        int first = Math.Clamp(firstVisible - margin, 0, pageCount - 1);
        int last = Math.Clamp(lastVisible + margin, 0, pageCount - 1);
        return (first, last);
    }

    /// <summary>Returns the page under the vertical centre of the viewport.</summary>
    /// <param name="offset">The vertical scroll offset in device-independent pixels.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    /// <param name="pageStride">The page height plus the gap between pages.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    public static int CentrePage(double offset, double viewportHeight, double pageStride, int pageCount)
    {
        if (pageCount <= 0 || pageStride <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Floor((Math.Max(0, offset) + (viewportHeight / 2)) / pageStride), 0, pageCount - 1);
    }
}
