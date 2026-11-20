using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>
/// The single <see cref="IEmbeddingProvider"/> binding (Sept-23 Phase 14, D-103):
/// delegates to the built-in ONNX provider, the local Ollama provider or nothing
/// (Off), and is the single answer to "can semantic search run". The choice is the
/// <c>OGMA_SEMANTIC_PROVIDER</c> administrator/test override when set, otherwise the
/// user's preference. <see cref="Changed"/> fires once per change of choice or
/// availability: on a preference change, on model install or removal, and on the
/// hourly refresh. It is raised on a background thread.
/// </summary>
public sealed class EmbeddingProviderRegistry : IEmbeddingProvider, ISemanticCapabilityService, IDisposable
{
    /// <summary>The administrator/test override variable (<c>builtin</c>, <c>ollama</c> or <c>off</c>).</summary>
    public const string EnvironmentVariable = "OGMA_SEMANTIC_PROVIDER";

    /// <summary>How often availability is re-probed without any other trigger.</summary>
    public static readonly TimeSpan RefreshPeriod = TimeSpan.FromHours(1);

    private const string Component = nameof(EmbeddingProviderRegistry);
    private const string OffProviderKey = "off";

    private readonly IEmbeddingProvider _builtIn;
    private readonly IEmbeddingProvider _ollama;
    private readonly IUserPreferencesService? _preferences;
    private readonly EmbeddingModelStore? _store;
    private readonly ILogger _logger;
    private readonly ITimer _timer;
    private readonly SemaphoreSlim _probeGate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly SemanticProviderChoice? _override;

    private SemanticProviderChoice _preferenceChoice = SemanticProviderChoice.BuiltIn;
    private bool _preferencesApplied;
    private volatile bool _available;
    private (SemanticProviderChoice Choice, bool Available) _published;
    private Task _pending = Task.CompletedTask;
    private bool _disposed;

    /// <summary>Initializes the registry.</summary>
    /// <param name="builtIn">The built-in ONNX provider.</param>
    /// <param name="ollama">The local Ollama provider.</param>
    /// <param name="preferences">Reads the persisted choice on first use when none was applied, or null.</param>
    /// <param name="store">The model store whose install and removal trigger a refresh, or null.</param>
    /// <param name="environmentOverride">Reads <see cref="EnvironmentVariable"/>; tests substitute it.</param>
    /// <param name="timeProvider">Clock for the hourly refresh; tests substitute it.</param>
    /// <param name="logger">Redacting logger, or null.</param>
    public EmbeddingProviderRegistry(
        IEmbeddingProvider builtIn,
        IOllamaEmbeddingProvider ollama,
        IUserPreferencesService? preferences = null,
        EmbeddingModelStore? store = null,
        Func<string?>? environmentOverride = null,
        TimeProvider? timeProvider = null,
        ILogger<EmbeddingProviderRegistry>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(builtIn);
        ArgumentNullException.ThrowIfNull(ollama);
        _builtIn = builtIn;
        _ollama = ollama;
        _preferences = preferences;
        _store = store;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _override = ParseOverride((environmentOverride ?? ReadEnvironment)());
        _published = (Choice, false);
        if (_store is not null)
        {
            _store.Changed += OnStoreChanged;
        }

        _timer = (timeProvider ?? TimeProvider.System).CreateTimer(
            _ => QueueRefresh(),
            null,
            RefreshPeriod,
            RefreshPeriod);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public SemanticProviderChoice Choice
    {
        get
        {
            lock (_sync)
            {
                return _override ?? _preferenceChoice;
            }
        }
    }

    /// <inheritdoc />
    public bool IsManagedByEnvironment => _override is not null;

    /// <inheritdoc />
    public bool IsAvailable => _available;

    /// <inheritdoc />
    public string ProviderKey => Current?.ProviderKey ?? OffProviderKey;

    /// <inheritdoc />
    public bool IsLocalOnly => Current?.IsLocalOnly ?? true;

    /// <inheritdoc />
    public string ModelId => Current?.ModelId ?? string.Empty;

    /// <inheritdoc />
    public string ModelVersion => Current?.ModelVersion ?? string.Empty;

    /// <inheritdoc />
    public int Dimensions => Current?.Dimensions ?? 0;

    private IEmbeddingProvider? Current => Resolve(Choice);

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => ProbeAndPublishAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> RefreshAsync(CancellationToken cancellationToken) => ProbeAndPublishAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<EmbeddingResult> EmbedAsync(
        string text,
        EmbeddingInputKind kind,
        CancellationToken cancellationToken)
    {
        await EnsurePreferencesAsync(cancellationToken).ConfigureAwait(false);
        IEmbeddingProvider provider = Current
            ?? throw new InvalidOperationException("Semantic search is switched off.");
        return await provider.EmbedAsync(text, kind, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ApplyPreferences(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        bool changed;
        lock (_sync)
        {
            _preferencesApplied = true;
            changed = _preferenceChoice != preferences.SemanticProvider;
            _preferenceChoice = preferences.SemanticProvider;
        }

        if (changed && _override is null)
        {
            QueueRefresh();
        }
    }

    /// <summary>Completes when every queued background refresh has finished.</summary>
    /// <returns>A task that completes when the registry is idle.</returns>
    public async Task WhenIdleAsync()
    {
        while (true)
        {
            Task pending;
            lock (_sync)
            {
                pending = _pending;
            }

            await pending.ConfigureAwait(false);
            lock (_sync)
            {
                if (ReferenceEquals(pending, _pending))
                {
                    return;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _timer.Dispose();
        if (_store is not null)
        {
            _store.Changed -= OnStoreChanged;
        }
    }

    private IEmbeddingProvider? Resolve(SemanticProviderChoice choice) => choice switch
    {
        SemanticProviderChoice.BuiltIn => _builtIn,
        SemanticProviderChoice.Ollama => _ollama,
        _ => null,
    };

    private void OnStoreChanged(object? sender, EventArgs e) => QueueRefresh();

    private void QueueRefresh()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _pending = _pending.ContinueWith(
                    _ => BackgroundRefreshAsync(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task BackgroundRefreshAsync()
    {
        try
        {
            await ProbeAndPublishAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A background refresh must never fault the queue; the last state stays published.
            InfrastructureLog.DegradedStep(_logger, exception, Component, "semantic.capability.refresh");
        }
    }

    private async Task<bool> ProbeAndPublishAsync(CancellationToken cancellationToken)
    {
        await EnsurePreferencesAsync(cancellationToken).ConfigureAwait(false);
        await _probeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SemanticProviderChoice choice = Choice;
            IEmbeddingProvider? provider = Resolve(choice);
            bool available = provider is not null
                && await provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
            _available = available;
            Publish(choice, available);
            return available;
        }
        finally
        {
            _probeGate.Release();
        }
    }

    private void Publish(SemanticProviderChoice choice, bool available)
    {
        lock (_sync)
        {
            if (_published == (choice, available))
            {
                return;
            }

            _published = (choice, available);
        }

        InfrastructureLog.SemanticCapabilityChanged(_logger, choice, available);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task EnsurePreferencesAsync(CancellationToken cancellationToken)
    {
        if (_preferences is null || _override is not null)
        {
            return;
        }

        lock (_sync)
        {
            if (_preferencesApplied)
            {
                return;
            }
        }

        UserPreferences preferences = await _preferences.GetAsync(cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            if (!_preferencesApplied)
            {
                _preferencesApplied = true;
                _preferenceChoice = preferences.SemanticProvider;
            }
        }
    }

    private static string? ReadEnvironment() => Environment.GetEnvironmentVariable(EnvironmentVariable);

    private static SemanticProviderChoice? ParseOverride(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "builtin" or "built-in" or "onnx" => SemanticProviderChoice.BuiltIn,
            "ollama" => SemanticProviderChoice.Ollama,
            "off" => SemanticProviderChoice.Off,
            _ => null,
        };
}
