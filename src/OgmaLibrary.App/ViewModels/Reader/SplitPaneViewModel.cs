using OgmaLibrary.App.Infrastructure;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>
/// One split-view pane: its own picker, its own <see cref="ReaderViewModel"/> and its own
/// session scope. Disposing the pane closes the book and releases the session (READ-012).
/// </summary>
public sealed class SplitPaneViewModel : IAsyncDisposable
{
    private readonly IDisposable? _scope;
    private bool _disposed;

    /// <summary>Initializes a new instance of <see cref="SplitPaneViewModel"/>.</summary>
    /// <param name="reader">The pane's reader view model.</param>
    /// <param name="picker">The pane's book picker.</param>
    /// <param name="scope">The session scope released on dispose; may be null in tests.</param>
    public SplitPaneViewModel(ReaderViewModel reader, BookPickerViewModel picker, IDisposable? scope)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(picker);
        Reader = reader;
        Picker = picker;
        _scope = scope;
        Picker.BookPicked += OnBookPicked;
    }

    /// <summary>The pane's independent reader.</summary>
    public ReaderViewModel Reader { get; }

    /// <summary>The pane's catalogue picker.</summary>
    public BookPickerViewModel Picker { get; }

    /// <summary>True once the pane has been disposed.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Opens <paramref name="bookId"/> in this pane.</summary>
    /// <param name="bookId">Catalogue book identity.</param>
    /// <param name="pageHint">Optional starting page.</param>
    /// <param name="cancellationToken">A token to cancel the open.</param>
    /// <returns>A task that completes when the book is open or has failed to open.</returns>
    public async Task OpenAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await Reader.OpenAsync(bookId, pageHint, cancellationToken).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Picker.BookPicked -= OnBookPicked;
        try
        {
            await Reader.CloseAsync(CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _scope?.Dispose();
        }
    }

    private void OnBookPicked(object? sender, BookPickerItem item) =>
        UiActions.Run(() => OpenAsync(item.BookId), "reader.split.open");
}
