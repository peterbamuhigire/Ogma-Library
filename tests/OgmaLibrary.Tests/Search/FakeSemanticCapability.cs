using OgmaLibrary.Application;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>Counting capability fake: proves the query path never probes (Sept-23 Phase 14, slice 4).</summary>
internal sealed class FakeSemanticCapability(bool available) : ISemanticCapabilityService
{
    private bool _available = available;

    public event EventHandler? Changed;

    public SemanticProviderChoice Choice => SemanticProviderChoice.BuiltIn;

    public bool IsManagedByEnvironment => false;

    public bool IsAvailable => _available;

    public int RefreshCalls { get; private set; }

    public TaskCompletionSource Refreshed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ApplyPreferences(UserPreferences preferences)
    {
    }

    public Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        RefreshCalls++;
        Refreshed.TrySetResult();
        return Task.FromResult(_available);
    }

    /// <summary>Flips availability and raises <see cref="Changed"/> on a pool thread, like the registry.</summary>
    public void Set(bool available)
    {
        _available = available;
        ThreadPool.QueueUserWorkItem(_ => Changed?.Invoke(this, EventArgs.Empty));
    }
}

/// <summary>Provider fake that counts every availability probe.</summary>
internal sealed class CountingProbeProvider(bool available) : FakeEmbeddingProvider
{
    public int ProbeCalls { get; private set; }

    public override Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        ProbeCalls++;
        return Task.FromResult(available);
    }

    protected override float[] CreateVector(string text, EmbeddingInputKind kind) => [1f, 0f, 0f];
}
