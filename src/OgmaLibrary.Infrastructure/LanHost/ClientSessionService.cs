using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>SQLite-backed LAN client session service that stores only token hashes.</summary>
internal sealed class ClientSessionService : IClientSessionService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    [ActivatorUtilitiesConstructor]
    public ClientSessionService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    internal ClientSessionService(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<ClientSessionResult> IssueAsync(
        ClientSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expires = now.Add(request.Lifetime);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);

        lease.Context.HostClientSessions.Add(new HostClientSessionRow
        {
            TokenHash = HashToken(token),
            ClientId = request.ClientId.Trim(),
            Role = request.Role.Trim(),
            IssuedUtc = now,
            ExpiresUtc = expires,
        });

        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ClientSessionResult(token, expires);
    }

    /// <inheritdoc />
    public async Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default) =>
        await GetActiveAsync(token, cancellationToken).ConfigureAwait(false) is not null;

    /// <inheritdoc />
    public async Task<ClientSessionSnapshot?> GetActiveAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string tokenHash = HashToken(token);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);

        HostClientSessionRow? session = await lease.Context.HostClientSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedUtc == null, cancellationToken)
            .ConfigureAwait(false);

        return session is not null && session.ExpiresUtc > now
            ? new ClientSessionSnapshot(
                TokenFingerprint: tokenHash[..16],
                ClientId: session.ClientId,
                Role: session.Role,
                ExpiresUtc: session.ExpiresUtc)
            : null;
    }

    /// <inheritdoc />
    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);

        List<DateTimeOffset> unrevokedExpiryTimes = await lease.Context.HostClientSessions
            .AsNoTracking()
            .Where(x => x.RevokedUtc == null)
            .Select(x => x.ExpiresUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return unrevokedExpiryTimes.Count(expires => expires > now);
    }

    /// <inheritdoc />
    public async Task RevokeAllAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);

        List<HostClientSessionRow> unrevoked = await lease.Context.HostClientSessions
            .Where(x => x.RevokedUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (HostClientSessionRow session in unrevoked.Where(x => x.ExpiresUtc > now))
        {
            session.RevokedUtc = now;
        }

        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static string HashToken(string token)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static void Validate(ClientSessionRequest request)
    {
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
    }
}
