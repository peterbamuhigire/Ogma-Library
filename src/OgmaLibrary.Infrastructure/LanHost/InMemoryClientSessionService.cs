using System.Security.Cryptography;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>In-memory LAN session service for the Phase 16 scaffold.</summary>
internal sealed class InMemoryClientSessionService : IClientSessionService
{
    private readonly Dictionary<string, DateTimeOffset> _sessions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ClientSessionResult> IssueAsync(
        ClientSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset expires = DateTimeOffset.UtcNow.Add(request.Lifetime);
        _sessions[token] = expires;
        return Task.FromResult(new ClientSessionResult(token, expires));
    }

    /// <inheritdoc />
    public Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(token) &&
            _sessions.TryGetValue(token, out DateTimeOffset expires) &&
            expires > DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Task.FromResult(_sessions.Values.Count(expires => expires > now));
    }

    /// <inheritdoc />
    public Task RevokeAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.Clear();
        return Task.CompletedTask;
    }
}

