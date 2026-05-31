using OgmaLibrary.Reader.Navigation;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Tests for <see cref="NavigationHistory"/> — FR-READ-002 navigation history.
/// </summary>
public sealed class NavigationHistoryTests
{
    [Fact]
    public void Push_SinglePage_CurrentIsCorrect()
    {
        var history = new NavigationHistory();
        history.Push(5);
        Assert.Equal(5, history.Current);
    }

    [Fact]
    public void Push_MultipleTimes_CurrentIsLastPushed()
    {
        var history = new NavigationHistory();
        history.Push(1);
        history.Push(10);
        history.Push(42);
        Assert.Equal(42, history.Current);
    }

    [Fact]
    public void GoBack_AfterMultiplePushes_ReturnsCorrectPages()
    {
        var history = new NavigationHistory();
        history.Push(0);
        history.Push(5);
        history.Push(10);

        int? back1 = history.GoBack(); // should be 5
        int? back2 = history.GoBack(); // should be 0

        Assert.Equal(5, back1);
        Assert.Equal(0, back2);
    }

    [Fact]
    public void GoBack_AtStart_ReturnsNull()
    {
        var history = new NavigationHistory();
        history.Push(0);
        Assert.Null(history.GoBack());
    }

    [Fact]
    public void GoForward_AfterGoBack_ReturnsForwardPage()
    {
        var history = new NavigationHistory();
        history.Push(0);
        history.Push(5);
        history.Push(10);

        history.GoBack();
        history.GoBack();
        int? forward = history.GoForward(); // should be 5
        Assert.Equal(5, forward);
    }

    [Fact]
    public void GoForward_AtEnd_ReturnsNull()
    {
        var history = new NavigationHistory();
        history.Push(0);
        history.Push(5);
        Assert.Null(history.GoForward());
    }

    [Fact]
    public void Push_AfterGoBack_DiscardsForwardStack()
    {
        var history = new NavigationHistory();
        history.Push(0);
        history.Push(5);
        history.Push(10);
        history.GoBack(); // at 5
        history.Push(20); // forward stack (10) discarded
        Assert.False(history.CanGoForward);
        Assert.Equal(20, history.Current);
    }

    [Fact]
    public void Exceeds_MaxDepth_EvictsOldestEntry()
    {
        var history = new NavigationHistory();

        // Push 55 pages (5 over the max depth of 50).
        for (int i = 0; i < 55; i++)
        {
            history.Push(i);
        }

        // Can only go back 49 times (50 entries total, at position 49).
        int backCount = 0;
        while (history.GoBack() is not null)
        {
            backCount++;
        }

        Assert.Equal(49, backCount);
    }

    [Fact]
    public void CanGoBack_EmptyHistory_ReturnsFalse()
    {
        var history = new NavigationHistory();
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void CanGoForward_EmptyHistory_ReturnsFalse()
    {
        var history = new NavigationHistory();
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Clear_ResetsHistory()
    {
        var history = new NavigationHistory();
        history.Push(1);
        history.Push(2);
        history.Push(3);
        history.Clear();
        Assert.Equal(-1, history.Current);
        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }
}
