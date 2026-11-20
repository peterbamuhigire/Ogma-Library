using OgmaLibrary.Application;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;
using OgmaLibrary.Infrastructure.Search;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Sept-23 Phase 14 slice 3: the registry is the single <see cref="IEmbeddingProvider"/>
/// and the single answer to "is semantic search available".
/// </summary>
public sealed class EmbeddingProviderRegistryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ogma-registry-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProvider _builtIn = new("builtin-onnx", "intfloat/multilingual-e5-small", 384);
    private readonly FakeProvider _ollama = new("ollama", "nomic-embed-text", 768);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Default_IsBuiltIn_AndUnavailableUntilTheModelIsInstalled()
    {
        using EmbeddingProviderRegistry registry = Create();

        Assert.Equal(SemanticProviderChoice.BuiltIn, registry.Choice);
        Assert.False(await registry.IsAvailableAsync(CancellationToken.None));
        Assert.False(registry.IsAvailable);
        Assert.Equal("builtin-onnx", registry.ProviderKey);
        Assert.Equal(384, registry.Dimensions);
        Assert.Equal(0, _ollama.ProbeCount);
    }

    [Fact]
    public async Task BuiltIn_AfterInstall_BecomesAvailable_AndChangedFiresOnce()
    {
        EmbeddingModelStore store = CreateStore();
        using EmbeddingProviderRegistry registry = Create(store: store);
        Assert.False(await registry.RefreshAsync(CancellationToken.None));
        int changes = 0;
        registry.Changed += (_, _) => Interlocked.Increment(ref changes);

        _builtIn.Available = true;
        store.Invalidate();
        Assert.True(await registry.RefreshAsync(CancellationToken.None));
        await registry.WhenIdleAsync();

        Assert.True(registry.IsAvailable);
        Assert.Equal(1, changes);

        EmbeddingResult result = await registry.EmbedAsync("photosynthesis", EmbeddingInputKind.Query, CancellationToken.None);
        Assert.Equal("builtin-onnx", result.ProviderKey);
        Assert.Equal(EmbeddingInputKind.Query, _builtIn.LastKind);

        _builtIn.Available = false;
        store.Invalidate();
        await registry.WhenIdleAsync();
        Assert.False(registry.IsAvailable);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Off_IsAlwaysUnavailable_AndNeverProbesOrEmbeds()
    {
        _builtIn.Available = true;
        _ollama.Available = true;
        using EmbeddingProviderRegistry registry = Create();
        registry.ApplyPreferences(new UserPreferences(SemanticProvider: SemanticProviderChoice.Off));
        await registry.WhenIdleAsync();
        int probes = _builtIn.ProbeCount + _ollama.ProbeCount;

        Assert.Equal(SemanticProviderChoice.Off, registry.Choice);
        Assert.False(await registry.IsAvailableAsync(CancellationToken.None));
        Assert.False(await registry.RefreshAsync(CancellationToken.None));
        Assert.Equal(probes, _builtIn.ProbeCount + _ollama.ProbeCount);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.EmbedAsync("x", EmbeddingInputKind.Query, CancellationToken.None));
    }

    [Fact]
    public async Task Ollama_DelegatesToTheOllamaProvider_AndChangedFiresOncePerTransition()
    {
        _ollama.Available = true;
        using EmbeddingProviderRegistry registry = Create();
        Assert.False(await registry.RefreshAsync(CancellationToken.None));
        int changes = 0;
        registry.Changed += (_, _) => Interlocked.Increment(ref changes);

        registry.ApplyPreferences(new UserPreferences(SemanticProvider: SemanticProviderChoice.Ollama));
        await registry.WhenIdleAsync();
        Assert.True(await registry.RefreshAsync(CancellationToken.None));

        Assert.Equal(1, changes);
        Assert.Equal("ollama", registry.ProviderKey);
        Assert.Equal("nomic-embed-text", registry.ModelId);
        Assert.Equal(768, registry.Dimensions);
        EmbeddingResult result = await registry.EmbedAsync("x", EmbeddingInputKind.Passage, CancellationToken.None);
        Assert.Equal("ollama", result.ProviderKey);

        registry.ApplyPreferences(new UserPreferences(SemanticProvider: SemanticProviderChoice.Ollama));
        registry.ApplyPreferences(new UserPreferences(SemanticProvider: SemanticProviderChoice.Off));
        await registry.WhenIdleAsync();
        Assert.Equal(2, changes);
        Assert.False(registry.IsAvailable);
    }

    [Theory]
    [InlineData("off", SemanticProviderChoice.Off)]
    [InlineData("OLLAMA", SemanticProviderChoice.Ollama)]
    [InlineData("builtin", SemanticProviderChoice.BuiltIn)]
    public async Task EnvironmentOverride_WinsOverThePreference(string value, SemanticProviderChoice expected)
    {
        using EmbeddingProviderRegistry registry = Create(environment: value);
        registry.ApplyPreferences(new UserPreferences(SemanticProvider: expected == SemanticProviderChoice.Off
            ? SemanticProviderChoice.Ollama
            : SemanticProviderChoice.Off));
        await registry.WhenIdleAsync();

        Assert.True(registry.IsManagedByEnvironment);
        Assert.Equal(expected, registry.Choice);
    }

    [Fact]
    public void EnvironmentOverride_UnknownValue_IsIgnored()
    {
        using EmbeddingProviderRegistry registry = Create(environment: "cloud");

        Assert.False(registry.IsManagedByEnvironment);
        Assert.Equal(SemanticProviderChoice.BuiltIn, registry.Choice);
    }

    [Fact]
    public async Task PersistedPreference_IsLoadedOnFirstUse()
    {
        _ollama.Available = true;
        var preferences = new FixedPreferences(new UserPreferences(SemanticProvider: SemanticProviderChoice.Ollama));
        using EmbeddingProviderRegistry registry = Create(preferences: preferences);

        Assert.True(await registry.IsAvailableAsync(CancellationToken.None));
        Assert.Equal(SemanticProviderChoice.Ollama, registry.Choice);
    }

    [Fact]
    public async Task HourlyRefresh_PicksUpAvailabilityChanges()
    {
        var time = new CapturingTimeProvider();
        using EmbeddingProviderRegistry registry = Create(time: time);
        Assert.False(await registry.RefreshAsync(CancellationToken.None));
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.Changed += (_, _) => changed.TrySetResult();

        Assert.Equal(TimeSpan.FromHours(1), time.Period);
        _builtIn.Available = true;
        time.Fire();

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(registry.IsAvailable);
    }

    [Fact]
    public async Task HangingProbe_IsCutOffByTheBudget_AndPublishedUnavailable()
    {
        _ollama.Hang = true;
        using EmbeddingProviderRegistry registry = Create();
        registry.ApplyPreferences(new UserPreferences(SemanticProvider: SemanticProviderChoice.Ollama));
        await registry.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var clock = System.Diagnostics.Stopwatch.StartNew();
        bool available = await registry.RefreshAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Task 14.6: the 1.5 s budget lives only inside the registry's refresh.
        Assert.False(available);
        Assert.False(registry.IsAvailable);
        Assert.True(clock.Elapsed < EmbeddingProviderRegistry.ProbeBudget + TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
    }

    private EmbeddingModelStore CreateStore()
    {
        EmbeddingModelManifest manifest = EmbeddingModelManifest.Parse(
            """
            {
              "modelId": "test/fake", "revision": "rev1", "licence": "MIT", "dimensions": 4, "maxTokens": 16,
              "queryPrefix": "", "passagePrefix": "", "source": "https://models.example.org/",
              "files": [
                { "role": "model", "path": "m.onnx", "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "sizeBytes": 1 },
                { "role": "tokenizer", "path": "t.model", "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "sizeBytes": 1 }
              ]
            }
            """);
        return new EmbeddingModelStore(_root, manifest);
    }

    private EmbeddingProviderRegistry Create(
        EmbeddingModelStore? store = null,
        string? environment = null,
        IUserPreferencesService? preferences = null,
        TimeProvider? time = null) =>
        new(_builtIn, _ollama, preferences, store, () => environment, time ?? new CapturingTimeProvider());

    private sealed class FakeProvider(string key, string modelId, int dimensions) : IOllamaEmbeddingProvider
    {
        private int _probeCount;

        public volatile bool Available;

        public volatile bool Hang;

        public int ProbeCount => _probeCount;

        public EmbeddingInputKind? LastKind { get; private set; }

        public string ProviderKey => key;

        public bool IsLocalOnly => true;

        public string ModelId => modelId;

        public string ModelVersion => "v1";

        public int Dimensions => dimensions;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _probeCount);
            return Hang ? new TaskCompletionSource<bool>().Task : Task.FromResult(Available);
        }

        public Task<EmbeddingResult> EmbedAsync(string text, EmbeddingInputKind kind, CancellationToken cancellationToken)
        {
            LastKind = kind;
            return Task.FromResult(new EmbeddingResult(key, modelId, "v1", new float[dimensions]));
        }
    }

    private sealed class FixedPreferences(UserPreferences value) : IUserPreferencesService
    {
        public Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);

        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingTimeProvider : TimeProvider
    {
        private TimerCallback? _callback;
        private object? _state;

        public TimeSpan Period { get; private set; }

        public void Fire() => _callback!(_state);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            Period = period;
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
