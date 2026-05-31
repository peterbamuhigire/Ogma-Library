using OgmaLibrary.Application.Commands;

namespace OgmaLibrary.Infrastructure.Commands;

/// <summary>
/// A ring-buffer implementation of <see cref="ICommandHistory"/> capped at 20 entries.
/// The oldest entry is silently dropped when the buffer is full (NFR-PROD-010).
/// Thread-safe via a <see cref="SemaphoreSlim"/>.
/// </summary>
public sealed class CommandHistory : ICommandHistory, IDisposable
{
    private const int MaxEntries = 20;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly LinkedList<IUndoableCommand> _stack = new();

    /// <inheritdoc />
    public bool CanUndo
    {
        get
        {
            _lock.Wait();
            try
            {
                return _stack.Count > 0;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        IUndoableCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stack.AddLast(command);
            while (_stack.Count > MaxEntries)
            {
                _stack.RemoveFirst();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();

    /// <inheritdoc />
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        IUndoableCommand? command;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stack.Count == 0)
            {
                return;
            }

            command = _stack.Last!.Value;
            _stack.RemoveLast();
        }
        finally
        {
            _lock.Release();
        }

        await command.UndoAsync(cancellationToken).ConfigureAwait(false);
    }
}
