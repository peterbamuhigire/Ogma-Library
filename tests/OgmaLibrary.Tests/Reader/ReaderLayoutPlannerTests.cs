using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;
using Xunit;

namespace OgmaLibrary.Tests.Reader;

public sealed class ReaderLayoutPlannerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(9, 8)]
    public void Spread_PairsOneTwoThreeFour(int page, int expectedStart) =>
        Assert.Equal(expectedStart, ReaderLayoutPlanner.SlotStart(DisplayMode.TwoPage, page, 10, coverAlone: false));

    [Fact]
    public void CoverAlone_ShowsPageZeroAloneThenPairs()
    {
        Assert.Equal([0], ReaderLayoutPlanner.PagesInSlot(DisplayMode.TwoPage, 0, 10, true));
        Assert.Equal([1, 2], ReaderLayoutPlanner.PagesInSlot(DisplayMode.TwoPage, 2, 10, true));
        Assert.Equal([3, 4], ReaderLayoutPlanner.PagesInSlot(DisplayMode.TwoPage, 3, 10, true));
    }

    [Fact]
    public void OddTotal_LastPageIsAlone()
    {
        Assert.Equal([4], ReaderLayoutPlanner.PagesInSlot(DisplayMode.TwoPage, 4, 5, false));
        Assert.Equal([3, 4], ReaderLayoutPlanner.PagesInSlot(DisplayMode.TwoPage, 4, 5, true));
        Assert.Equal([5], ReaderLayoutPlanner.PagesInSlot(DisplayMode.TwoPage, 5, 6, true));
    }

    [Fact]
    public void LastPage_HasNoNextSlot_AndFirstHasNoPrevious()
    {
        Assert.Null(ReaderLayoutPlanner.NextSlotStart(DisplayMode.TwoPage, 9, 10, false));
        Assert.Null(ReaderLayoutPlanner.NextSlotStart(DisplayMode.TwoPage, 9, 10, true));
        Assert.Equal(2, ReaderLayoutPlanner.NextSlotStart(DisplayMode.TwoPage, 0, 10, false));
        Assert.Equal(1, ReaderLayoutPlanner.NextSlotStart(DisplayMode.TwoPage, 0, 10, true));
        Assert.Null(ReaderLayoutPlanner.PreviousSlotStart(DisplayMode.TwoPage, 1, 10, false));
        Assert.Equal(0, ReaderLayoutPlanner.PreviousSlotStart(DisplayMode.TwoPage, 1, 10, true));
        Assert.Equal(1, ReaderLayoutPlanner.PreviousSlotStart(DisplayMode.TwoPage, 3, 10, true));
    }

    [Theory]
    [InlineData(DisplayMode.SinglePage, 10, false, 10)]
    [InlineData(DisplayMode.Continuous, 10, true, 10)]
    [InlineData(DisplayMode.TwoPage, 10, false, 5)]
    [InlineData(DisplayMode.TwoPage, 10, true, 6)]
    [InlineData(DisplayMode.TwoPage, 9, true, 5)]
    [InlineData(DisplayMode.TwoPage, 0, true, 0)]
    public void SlotCount_MatchesPairing(DisplayMode mode, int pages, bool cover, int expected) =>
        Assert.Equal(expected, ReaderLayoutPlanner.SlotCount(mode, pages, cover));

    [Fact]
    public void SinglePage_IsOnePagePerSlot() =>
        Assert.Equal([3], ReaderLayoutPlanner.PagesInSlot(DisplayMode.SinglePage, 3, 10, true));

    [Fact]
    public void RealisedRange_IsVisiblePlusTwo()
    {
        (int first, int last) = ReaderLayoutPlanner.RealisedRange(50_000, 1000, 1000, 100, 2);
        Assert.Equal((48, 52), (first, last));
        Assert.Equal((0, 2), ReaderLayoutPlanner.RealisedRange(0, 1000, 1000, 100, 2));
        Assert.Equal((97, 99), ReaderLayoutPlanner.RealisedRange(99_000, 1000, 1000, 100, 2));
        Assert.Equal(50, ReaderLayoutPlanner.CentrePage(50_000, 1000, 1000, 100));
    }
}
