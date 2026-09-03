using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>Bounded in-process quota and circuit-breaker state for providers.</summary>
public sealed class MetadataProviderHealth : IMetadataProviderHealth
{
    private static readonly TimeSpan QuotaWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CircuitDuration = TimeSpan.FromSeconds(30);
    private const int MaxRequestsPerWindow = 120;
    private const int FailureThreshold = 3;
    private readonly object _gate = new();
    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool TryReserve(string provider)
    {
        State state = GetOrCreate(provider);
        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            RollWindow(state, now);
            if (state.CircuitOpenUntilUtc is { } until && until > now)
            {
                state.WindowRejected++;
                return false;
            }

            if (state.WindowRequests >= MaxRequestsPerWindow)
            {
                state.WindowRejected++;
                return false;
            }

            state.WindowRequests++;
            return true;
        }
    }

    /// <inheritdoc />
    public void RecordSuccess(string provider)
    {
        State state = GetOrCreate(provider);
        lock (_gate)
        {
            state.ConsecutiveFailures = 0;
            state.CircuitOpenUntilUtc = null;
        }
    }

    /// <inheritdoc />
    public void RecordFailure(string provider)
    {
        State state = GetOrCreate(provider);
        lock (_gate)
        {
            state.TotalFailures++;
            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures >= FailureThreshold)
            {
                state.CircuitOpenUntilUtc = DateTimeOffset.UtcNow.Add(CircuitDuration);
            }
        }
    }

    /// <inheritdoc />
    public MetadataProviderHealthSnapshot GetSnapshot(string provider)
    {
        State state = GetOrCreate(provider);
        lock (_gate)
        {
            RollWindow(state, DateTimeOffset.UtcNow);
            return new MetadataProviderHealthSnapshot(
                state.Provider,
                state.WindowRequests,
                state.WindowRejected,
                state.ConsecutiveFailures,
                state.TotalFailures,
                state.TotalRetries,
                state.WindowStartedUtc,
                state.CircuitOpenUntilUtc);
        }
    }

    /// <inheritdoc />
    public void RecordRetry(string provider)
    {
        State state = GetOrCreate(provider);
        lock (_gate)
        {
            state.TotalRetries++;
        }
    }

    private State GetOrCreate(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        lock (_gate)
        {
            if (!_states.TryGetValue(provider.Trim(), out State? state))
            {
                state = new State(provider.Trim());
                _states.Add(state.Provider, state);
            }

            return state;
        }
    }

    private static void RollWindow(State state, DateTimeOffset now)
    {
        if (now - state.WindowStartedUtc < QuotaWindow)
        {
            return;
        }

        state.WindowStartedUtc = now;
        state.WindowRequests = 0;
        state.WindowRejected = 0;
    }

    private sealed class State(string provider)
    {
        public string Provider { get; } = provider;
        public long WindowRequests { get; set; }
        public long WindowRejected { get; set; }
        public long ConsecutiveFailures { get; set; }
        public long TotalFailures { get; set; }
        public long TotalRetries { get; set; }
        public DateTimeOffset WindowStartedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CircuitOpenUntilUtc { get; set; }
    }
}
