using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// The process-wide <see cref="ICatalogueReadiness"/>: opened by <see cref="CatalogueMigrator"/>
/// after a successful migration and never closed again.
/// </summary>
public sealed class CatalogueReadinessGate : ICatalogueReadiness
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public bool IsReady => _ready.Task.IsCompleted;

    /// <summary>Marks the catalogue ready and releases every waiter.</summary>
    public void MarkReady() => _ready.TrySetResult();

    /// <inheritdoc />
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default) =>
        _ready.Task.WaitAsync(cancellationToken);
}
