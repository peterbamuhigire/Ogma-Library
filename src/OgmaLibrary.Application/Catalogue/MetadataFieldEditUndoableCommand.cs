using OgmaLibrary.Application.Commands;

namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// One inline metadata edit from the book inspector that can be undone (Sept-23 Phase 11,
/// task 11.4). Execute captures the field's before-state, then writes the new value; Undo
/// restores the captured state. Undo runs at most once, so the inspector's Undo button and the
/// shared command history cannot restore the same edit twice.
/// </summary>
public sealed class MetadataFieldEditUndoableCommand : IUndoableCommand
{
    private readonly ICatalogueWriteService _writeService;
    private MetadataFieldSnapshot? _before;

    /// <summary>Initializes a new instance of the <see cref="MetadataFieldEditUndoableCommand"/> class.</summary>
    /// <param name="writeService">The catalogue write service.</param>
    /// <param name="bookId">The book to edit.</param>
    /// <param name="fieldName">The metadata field name.</param>
    /// <param name="value">The new value, or <see langword="null"/> to clear the field.</param>
    public MetadataFieldEditUndoableCommand(
        ICatalogueWriteService writeService,
        string bookId,
        string fieldName,
        string? value)
    {
        ArgumentNullException.ThrowIfNull(writeService);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        _writeService = writeService;
        BookId = bookId;
        FieldName = fieldName;
        Value = value;
    }

    /// <summary>The edited book.</summary>
    public string BookId { get; }

    /// <summary>The edited field.</summary>
    public string FieldName { get; }

    /// <summary>The value written by <see cref="ExecuteAsync"/>.</summary>
    public string? Value { get; }

    /// <summary>Whether the edit has run and has not been undone.</summary>
    public bool CanUndo => _before is not null;

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        MetadataFieldSnapshot? before = await _writeService
            .CaptureMetadataFieldSnapshotAsync(BookId, FieldName, cancellationToken)
            .ConfigureAwait(false);
        await _writeService
            .UpdateMetadataFieldAsync(BookId, FieldName, Value, cancellationToken)
            .ConfigureAwait(false);
        _before = before;
    }

    /// <inheritdoc />
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        MetadataFieldSnapshot? before = Interlocked.Exchange(ref _before, null);
        if (before is null)
        {
            return;
        }

        try
        {
            await _writeService.RestoreMetadataFieldSnapshotAsync(before, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The restore did not complete, so the edit is still undoable.
            Interlocked.CompareExchange(ref _before, before, null);
            throw;
        }
    }
}
