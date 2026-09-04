using System.Net.Http;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Bounded provider-call resilience settings.</summary>
public sealed record AiProviderResilienceOptions
{
    /// <summary>Default settings for interactive provider calls.</summary>
    public static AiProviderResilienceOptions Default { get; } = new();

    /// <summary>Maximum attempts for one provider call, including the first attempt.</summary>
    public int MaxAttempts { get; init; } = 2;

    /// <summary>Maximum duration of one provider attempt.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Failures required to open the provider circuit.</summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>Time the provider circuit remains open.</summary>
    public TimeSpan CircuitOpenDuration { get; init; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (MaxAttempts is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts));
        }

        if (AttemptTimeout <= TimeSpan.Zero || AttemptTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(AttemptTimeout));
        }

        if (FailureThreshold is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(FailureThreshold));
        }

        if (CircuitOpenDuration <= TimeSpan.Zero || CircuitOpenDuration > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(CircuitOpenDuration));
        }
    }
}

/// <summary>Observable bounded provider health state.</summary>
public sealed record AiProviderHealthSnapshot(
    string ProviderKey,
    int ConsecutiveFailures,
    int TotalFailures,
    int TotalRetries,
    DateTimeOffset? CircuitOpenUntilUtc);

/// <summary>Per-provider health tracker used by the resilience decorator.</summary>
public sealed class AiProviderHealthRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAiProviderHealthStore? _store;

    /// <summary>Initializes the health registry and restores redacted state when available.</summary>
    public AiProviderHealthRegistry(IAiProviderHealthStore? store = null)
    {
        _store = store;
        foreach (AiProviderHealthSnapshot snapshot in store?.Load() ?? [])
        {
            if (string.IsNullOrWhiteSpace(snapshot.ProviderKey))
            {
                continue;
            }

            _states[snapshot.ProviderKey.Trim()] = new State
            {
                ConsecutiveFailures = Math.Max(0, snapshot.ConsecutiveFailures),
                TotalFailures = Math.Max(0, snapshot.TotalFailures),
                TotalRetries = Math.Max(0, snapshot.TotalRetries),
                CircuitOpenUntilUtc = snapshot.CircuitOpenUntilUtc,
            };
        }
    }

    internal bool IsCircuitOpen(string providerKey)
    {
        State state = GetOrCreate(providerKey);
        lock (_gate)
        {
            return state.CircuitOpenUntilUtc is { } until && until > DateTimeOffset.UtcNow;
        }
    }

    internal void RecordSuccess(string providerKey)
    {
        State state = GetOrCreate(providerKey);
        lock (_gate)
        {
            state.ConsecutiveFailures = 0;
            state.CircuitOpenUntilUtc = null;
            PersistLocked();
        }
    }

    internal void RecordFailure(string providerKey, AiProviderResilienceOptions options)
    {
        State state = GetOrCreate(providerKey);
        lock (_gate)
        {
            state.ConsecutiveFailures++;
            state.TotalFailures++;
            if (state.ConsecutiveFailures >= options.FailureThreshold)
            {
                state.CircuitOpenUntilUtc = DateTimeOffset.UtcNow.Add(options.CircuitOpenDuration);
            }

            PersistLocked();
        }
    }

    internal void RecordRetry(string providerKey)
    {
        State state = GetOrCreate(providerKey);
        lock (_gate)
        {
            state.TotalRetries++;
            PersistLocked();
        }
    }

    /// <summary>Returns a point-in-time provider health snapshot.</summary>
    public AiProviderHealthSnapshot GetSnapshot(string providerKey)
    {
        State state = GetOrCreate(providerKey);
        lock (_gate)
        {
            DateTimeOffset? circuit = state.CircuitOpenUntilUtc is { } until && until > DateTimeOffset.UtcNow
                ? until
                : null;
            return new AiProviderHealthSnapshot(
                providerKey.Trim(),
                state.ConsecutiveFailures,
                state.TotalFailures,
                state.TotalRetries,
                circuit);
        }
    }

    private State GetOrCreate(string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        lock (_gate)
        {
            if (!_states.TryGetValue(providerKey.Trim(), out State? state))
            {
                state = new State();
                _states.Add(providerKey.Trim(), state);
            }

            return state;
        }
    }

    private void PersistLocked()
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            _store.Save(_states.Select(pair => new AiProviderHealthSnapshot(
                pair.Key,
                pair.Value.ConsecutiveFailures,
                pair.Value.TotalFailures,
                pair.Value.TotalRetries,
                pair.Value.CircuitOpenUntilUtc)).ToArray());
        }
        catch (IOException)
        {
            // Health persistence must never make a provider call fail open or
            // turn an otherwise isolated provider failure into app failure.
        }
        catch (UnauthorizedAccessException)
        {
            // The next process start can safely reconstruct an empty snapshot.
        }
    }

    private sealed class State
    {
        public int ConsecutiveFailures { get; set; }

        public int TotalFailures { get; set; }

        public int TotalRetries { get; set; }

        public DateTimeOffset? CircuitOpenUntilUtc { get; set; }
    }
}

/// <summary>Applies timeout, transient retry, and circuit-breaker controls to a provider.</summary>
public sealed class ResilientAiProvider : IAiProvider
{
    private readonly IAiProvider _inner;
    private readonly AiProviderHealthRegistry _health;
    private readonly AiProviderResilienceOptions _options;

    /// <summary>Initializes the resilience decorator.</summary>
    public ResilientAiProvider(
        IAiProvider inner,
        AiProviderHealthRegistry health,
        AiProviderResilienceOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _options = options ?? AiProviderResilienceOptions.Default;
        _options.Validate();
    }

    /// <inheritdoc />
    public string ProviderKey => _inner.ProviderKey;

    /// <inheritdoc />
    public bool IsLocalOnly => _inner.IsLocalOnly;

    /// <inheritdoc />
    public async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_health.IsCircuitOpen(ProviderKey))
        {
            throw new InvalidOperationException($"AI provider circuit is open for '{ProviderKey}'.");
        }

        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.AttemptTimeout);
            try
            {
                AiCompletion completion = await _inner.CompleteAsync(request, timeout.Token).ConfigureAwait(false);
                _health.RecordSuccess(ProviderKey);
                return completion;
            }
            catch (HttpRequestException) when (attempt < _options.MaxAttempts)
            {
                _health.RecordRetry(ProviderKey);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < _options.MaxAttempts)
            {
                _health.RecordRetry(ProviderKey);
            }
            catch
            {
                _health.RecordFailure(ProviderKey, _options);
                throw;
            }
        }

        throw new InvalidOperationException("AI provider resilience loop exited unexpectedly.");
    }
}
