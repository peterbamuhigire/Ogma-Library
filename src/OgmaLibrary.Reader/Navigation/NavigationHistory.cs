namespace OgmaLibrary.Reader.Navigation;

/// <summary>
/// A bounded navigation history stack for the PDF reader (FR-READ-002).
/// Supports back/forward without pushing new entries during history traversal.
/// Maximum depth: 50 entries.
/// </summary>
public sealed class NavigationHistory
{
    private const int MaxDepth = 50;

    private readonly List<int> _history = new();
    private int _position = -1; // Index into _history pointing at current page.

    /// <summary>Whether the back action is available.</summary>
    public bool CanGoBack => _position > 0;

    /// <summary>Whether the forward action is available.</summary>
    public bool CanGoForward => _position < _history.Count - 1;

    /// <summary>The current page index from the history, or -1 if empty.</summary>
    public int Current => _position >= 0 && _position < _history.Count
        ? _history[_position]
        : -1;

    /// <summary>
    /// Pushes a new page onto the history stack. Entries beyond the current position
    /// (i.e., the forward stack) are discarded before adding. The oldest entry is
    /// evicted when the stack exceeds <see cref="MaxDepth"/>.
    /// </summary>
    /// <param name="pageIndex">The page to push.</param>
    public void Push(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        // Discard forward stack.
        if (_position < _history.Count - 1)
        {
            _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        }

        _history.Add(pageIndex);
        _position = _history.Count - 1;

        // Evict oldest when over budget.
        while (_history.Count > MaxDepth)
        {
            _history.RemoveAt(0);
            _position--;
        }
    }

    /// <summary>
    /// Moves back one entry in the history without pushing a new entry.
    /// </summary>
    /// <returns>
    /// The page index to navigate to, or <see langword="null"/> if at the beginning.
    /// </returns>
    public int? GoBack()
    {
        if (!CanGoBack)
        {
            return null;
        }

        _position--;
        return _history[_position];
    }

    /// <summary>
    /// Moves forward one entry in the history without pushing a new entry.
    /// </summary>
    /// <returns>
    /// The page index to navigate to, or <see langword="null"/> if at the end.
    /// </returns>
    public int? GoForward()
    {
        if (!CanGoForward)
        {
            return null;
        }

        _position++;
        return _history[_position];
    }

    /// <summary>Clears the history stack.</summary>
    public void Clear()
    {
        _history.Clear();
        _position = -1;
    }
}
