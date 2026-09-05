using System.Security.Cryptography;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Deterministic in-memory LAN session service used by tests.</summary>
internal sealed class InMemoryClientSessionService : IClientSessionService
{
    private readonly Dictionary<string, MemorySession> _sessions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ClientSessionResult> IssueAsync(
        ClientSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new ArgumentException("Client id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            throw new ArgumentException("Client role is required.", nameof(request));
        }

        if (request.Lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Lifetime, "Client session lifetime must be positive.");
        }

        string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset expires = DateTimeOffset.UtcNow.Add(request.Lifetime);
        _sessions[token] = new MemorySession(request.ClientId.Trim(), request.Role.Trim(), expires);
        return Task.FromResult(new ClientSessionResult(token, expires));
    }

    /// <inheritdoc />
    public Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryGetActive(token, out _));
    }

    /// <inheritdoc />
    public Task<ClientSessionSnapshot?> GetActiveAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            TryGetActive(token, out ClientSessionSnapshot? snapshot) ? snapshot : null);
    }

    /// <inheritdoc />
    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Task.FromResult(_sessions.Values.Count(session => session.ExpiresUtc > now));
    }

    /// <inheritdoc />
    public Task RevokeAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.Clear();
        return Task.CompletedTask;
    }

    private bool TryGetActive(string token, out ClientSessionSnapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(token) ||
            !_sessions.TryGetValue(token, out MemorySession? session) ||
            session.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        string fingerprint = ClientSessionService.HashToken(token)[..16];
        snapshot = new ClientSessionSnapshot(fingerprint, session.ClientId, session.Role, session.ExpiresUtc);
        return true;
    }

    private sealed record MemorySession(string ClientId, string Role, DateTimeOffset ExpiresUtc);
}

