using System.Text.Json;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Persisted UTC-day usage state for the local AI budget.</summary>
public sealed record AiUsageBudgetState(
    DateOnly UtcDate,
    int ReservedTokens,
    int UsedTokens,
    decimal UsedCostUsd);

/// <summary>Persistence boundary for daily AI usage state.</summary>
public interface IAiUsageBudgetStore
{
    /// <summary>Loads state for the current process.</summary>
    AiUsageBudgetState? Load();

    /// <summary>Atomically persists daily usage state.</summary>
    void Save(AiUsageBudgetState state);
}

/// <summary>Atomic JSON persistence for the redacted daily AI usage state.</summary>
public sealed class JsonAiUsageBudgetStore : IAiUsageBudgetStore
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly string _path;

    /// <summary>Initializes the store at an application-data path.</summary>
    public JsonAiUsageBudgetStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <inheritdoc />
    public AiUsageBudgetState? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            BudgetDocument? document = JsonSerializer.Deserialize<BudgetDocument>(
                File.ReadAllText(_path),
                JsonOptions);
            return document?.Version == CurrentVersion
                ? document.State
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Save(AiUsageBudgetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(new BudgetDocument(CurrentVersion, state), JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record BudgetDocument(int Version, AiUsageBudgetState State);
}

/// <summary>Single-process atomic budget enforcement backed by durable daily state.</summary>
public sealed class AiUsageBudgetService : IAiUsageBudgetService
{
    private const int MaximumProviderCompletionTokens = 1_024;
    private readonly object _gate = new();
    private readonly IAiUsageBudgetStore _store;
    private readonly AiUsageBudgetLimits _limits;
    private readonly Dictionary<string, int> _reservations = new(StringComparer.Ordinal);
    private AiUsageBudgetState _state;

    /// <summary>Initializes the budget service and restores the current UTC-day state.</summary>
    public AiUsageBudgetService(
        IAiUsageBudgetStore store,
        AiUsageBudgetLimits? limits = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _limits = limits ?? new AiUsageBudgetLimits();
        _limits.Validate();
        _state = NormalizeDate(store.Load());
    }

    /// <inheritdoc />
    public Task<AiUsageBudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RefreshDateLocked();
            return Task.FromResult(ToSnapshot());
        }
    }

    /// <inheritdoc />
    public Task<AiUsageBudgetReservation> ReserveAsync(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        int estimatedTokens = EstimateTokens(request);
        lock (_gate)
        {
            RefreshDateLocked();
            if (_state.UsedCostUsd >= _limits.DailyCostUsdLimit)
            {
                throw new AiUsageBudgetExceededException("The daily AI cost budget has been exhausted.");
            }

            if ((long)_state.ReservedTokens + _state.UsedTokens + estimatedTokens > _limits.DailyTokenLimit)
            {
                throw new AiUsageBudgetExceededException("The daily AI token budget would be exceeded.");
            }

            string id = $"aibudget-{Guid.NewGuid():N}";
            _reservations.Add(id, estimatedTokens);
            _state = _state with { ReservedTokens = _state.ReservedTokens + estimatedTokens };
            PersistLocked();
            return Task.FromResult(new AiUsageBudgetReservation(id, estimatedTokens));
        }
    }

    /// <inheritdoc />
    public Task FinalizeAsync(
        AiUsageBudgetReservation reservation,
        AiCompletion completion,
        decimal? costUsd,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(completion);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RefreshDateLocked();
            if (!_reservations.Remove(reservation.Id, out int reservedTokens))
            {
                return Task.CompletedTask;
            }

            int actualTokens = Math.Max(
                0,
                (completion.PromptTokens ?? 0) + (completion.CompletionTokens ?? 0));
            actualTokens = actualTokens == 0 ? reservedTokens : actualTokens;
            _state = _state with
            {
                ReservedTokens = Math.Max(0, _state.ReservedTokens - reservedTokens),
                UsedTokens = checked(_state.UsedTokens + actualTokens),
                UsedCostUsd = _state.UsedCostUsd + Math.Max(0m, costUsd ?? 0m),
            };
            PersistLocked();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(
        AiUsageBudgetReservation reservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RefreshDateLocked();
            if (_reservations.Remove(reservation.Id, out int reservedTokens))
            {
                _state = _state with
                {
                    ReservedTokens = Math.Max(0, _state.ReservedTokens - reservedTokens),
                };
                PersistLocked();
            }
        }

        return Task.CompletedTask;
    }

    private static int EstimateTokens(AiRequest request)
    {
        long characters = request.QueryType.Length + (request.QueryText?.Length ?? 0);
        characters += request.MetadataFields.Sum(field => field.Key.Length + field.Value.Length);
        characters += request.ContentChunks.Sum(chunk => chunk.BookId.Length + chunk.Source.Length + chunk.Text.Length);
        long estimate = (long)Math.Ceiling(characters / 4d) + MaximumProviderCompletionTokens;
        return (int)Math.Clamp(estimate, 1, int.MaxValue);
    }

    private static AiUsageBudgetState NormalizeDate(AiUsageBudgetState? state) =>
        state is { } value && value.UtcDate == CurrentDate()
            ? value with
            {
                ReservedTokens = Math.Max(0, value.ReservedTokens),
                UsedTokens = Math.Max(0, value.UsedTokens),
                UsedCostUsd = Math.Max(0m, value.UsedCostUsd),
            }
            : new AiUsageBudgetState(CurrentDate(), 0, 0, 0m);

    private void RefreshDateLocked()
    {
        if (_state.UtcDate != CurrentDate())
        {
            _reservations.Clear();
            _state = new AiUsageBudgetState(CurrentDate(), 0, 0, 0m);
            PersistLocked();
        }
    }

    private AiUsageBudgetSnapshot ToSnapshot() =>
        new(_state.UtcDate, _state.ReservedTokens, _state.UsedTokens, _state.UsedCostUsd, _limits);

    private void PersistLocked()
    {
        try
        {
            _store.Save(_state);
        }
        catch (IOException)
        {
            // Losing telemetry continuity must not weaken the in-memory gate.
        }
        catch (UnauthorizedAccessException)
        {
            // The in-memory gate remains authoritative for this process.
        }
    }

    private static DateOnly CurrentDate() => DateOnly.FromDateTime(DateTime.UtcNow);
}
