using OgmaLibrary.Application.Commands;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// A bulk edit that can be undone (Sept-23 Phase 10, task 10.8, CAT-005). Execute captures every
/// named book's before-state, then applies the edit; Undo restores the captured state.
/// </summary>
public sealed class BulkEditUndoableCommand : IUndoableCommand
{
    private readonly ICatalogueWriteService _writeService;
    private IReadOnlyList<BulkEditSnapshot>? _before;

    /// <summary>Initializes a new instance of the <see cref="BulkEditUndoableCommand"/> class.</summary>
    /// <param name="writeService">The catalogue write service.</param>
    /// <param name="command">The bulk edit to apply.</param>
    public BulkEditUndoableCommand(ICatalogueWriteService writeService, BulkEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(writeService);
        ArgumentNullException.ThrowIfNull(command);
        _writeService = writeService;
        Command = command;
    }

    /// <summary>The bulk edit.</summary>
    public BulkEditCommand Command { get; }

    /// <summary>Whether the edit has run and can be undone.</summary>
    public bool HasExecuted => _before is not null;

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BulkEditSnapshot> before = await _writeService
            .CaptureBulkEditSnapshotAsync(Command.BookIds, cancellationToken)
            .ConfigureAwait(false);
        await _writeService.BulkEditAsync(Command, cancellationToken).ConfigureAwait(false);
        _before = before;
    }

    /// <inheritdoc />
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        if (_before is null)
        {
            return;
        }

        await _writeService.RestoreBulkEditSnapshotAsync(_before, cancellationToken).ConfigureAwait(false);
        _before = null;
    }
}
