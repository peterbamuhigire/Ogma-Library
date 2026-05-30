using System.Collections.Concurrent;
using System.Diagnostics;
using OgmaLibrary.Application;

namespace OgmaLibrary.Infrastructure;

/// <summary>
/// A <see cref="IBenchmarkContext"/> backed by <see cref="Stopwatch"/>. This is the
/// only place in the solution permitted to reference <see cref="Stopwatch"/>; all
/// other code measures through the <see cref="IBenchmarkContext"/> contract.
/// </summary>
public sealed class StopwatchBenchmarkContext : IBenchmarkContext
{
    private readonly ConcurrentDictionary<string, TimeSpan> _lastDurations = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IDisposable Measure(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return new Scope(this, operationName);
    }

    /// <inheritdoc />
    public TimeSpan GetLastDuration(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return _lastDurations.TryGetValue(operationName, out TimeSpan value) ? value : TimeSpan.Zero;
    }

    private sealed class Scope : IDisposable
    {
        private readonly StopwatchBenchmarkContext _owner;
        private readonly string _operationName;
        private readonly long _startTimestamp;

        public Scope(StopwatchBenchmarkContext owner, string operationName)
        {
            _owner = owner;
            _operationName = operationName;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose() =>
            _owner._lastDurations[_operationName] = Stopwatch.GetElapsedTime(_startTimestamp);
    }
}
