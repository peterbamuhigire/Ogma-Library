using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// In-memory <see cref="ISemanticIndexControl"/> shared by Settings and the embedding worker
/// (Sept-23 Phase 14, slice 4). The pause flag lives for the process only (D-113).
/// </summary>
public sealed class SemanticIndexControl : ISemanticIndexControl
{
    private readonly ISemanticCapabilityService? _capability;
    private volatile bool _paused;

    /// <summary>Initializes the control.</summary>
    /// <param name="capability">Re-checked by <see cref="BuildNowAsync"/>, or null.</param>
    public SemanticIndexControl(ISemanticCapabilityService? capability = null)
    {
        _capability = capability;
    }

    /// <inheritdoc />
    public bool IsPaused => _paused;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Pause() => Set(paused: true);

    /// <inheritdoc />
    public void ResumeIndexing() => Set(paused: false);

    /// <inheritdoc />
    public async Task BuildNowAsync(CancellationToken cancellationToken)
    {
        _paused = false;
        if (_capability is not null)
        {
            await _capability.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        _paused = paused;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
