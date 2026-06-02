using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>In-memory Host trust store until OS-backed credential storage lands.</summary>
internal sealed class InMemoryHostTrustStore : IHostTrustStore
{
    private readonly Dictionary<string, HostTrustPin> _pins = new(StringComparer.OrdinalIgnoreCase);

    public Task<HostTrustPin?> GetAsync(string hostKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostKey);
        cancellationToken.ThrowIfCancellationRequested();
        _pins.TryGetValue(hostKey, out HostTrustPin? pin);
        return Task.FromResult(pin);
    }

    public Task SaveAsync(HostTrustPin pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);
        cancellationToken.ThrowIfCancellationRequested();
        _pins[pin.HostKey] = pin;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string hostKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostKey);
        cancellationToken.ThrowIfCancellationRequested();
        _pins.Remove(hostKey);
        return Task.CompletedTask;
    }
}
