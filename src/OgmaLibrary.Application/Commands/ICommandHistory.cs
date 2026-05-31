namespace OgmaLibrary.Application.Commands;

/// <summary>
/// An undoable command that can be reversed after execution (NFR-PROD-010).
/// Implementations carry enough state to reconstruct the before-state.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Executes the command.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>Undoes the command, restoring the state before <see cref="ExecuteAsync"/>.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UndoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A ring-buffer history of undoable commands (NFR-PROD-010, FR-CAT-005).
/// The buffer is capped at 20 entries; the oldest entry is dropped when the
/// buffer is full. Undo is LIFO.
/// </summary>
public interface ICommandHistory
{
    /// <summary>
    /// Executes <paramref name="command"/> and pushes it onto the undo stack.
    /// </summary>
    /// <param name="command">The command to execute and record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ExecuteAsync(IUndoableCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes the most-recently executed command, if the history is not empty.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UndoAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the undo stack has at least one entry.</summary>
    bool CanUndo { get; }
}
