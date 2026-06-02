using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>SQLite-backed school-managed classroom profile enrollment service.</summary>
internal sealed class SchoolProfileEnrollmentService : IProfileEnrollmentService
{
    private static readonly HashSet<string> EnrollableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "student",
        "teacher",
    };

    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;

    public SchoolProfileEnrollmentService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<EnrollmentToken> EnrollAsync(
        EnrollProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string displayName = RequireTrimmed(request.DisplayName, nameof(request.DisplayName));
        string role = NormalizeRole(request.Role);
        Guid profileId = Guid.NewGuid();
        string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expires = now.AddHours(24);

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            context.EnrolledProfiles.Add(new EnrolledProfileRow
            {
                ProfileId = profileId.ToString("D"),
                DisplayName = displayName,
                Role = role,
                BirthYear = request.BirthYear,
                EnrollmentToken = HashToken(token),
                EnrollmentTokenExpiresUtc = expires,
                EnrolledUtc = now,
            });
            context.SchoolAiEntitlements.Add(new SchoolAiEntitlementRow
            {
                ProfileId = profileId.ToString("D"),
                DailyTokenBudget = 10_000,
                ClassDailyTokenBudget = 500_000,
                RateLimitQueriesPerMin = 5,
                UpdatedUtc = now,
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new EnrollmentToken(profileId, token, expires);
    }

    public async Task<IReadOnlyList<EnrolledProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            List<EnrolledProfileRow> rows = await context.EnrolledProfiles
                .AsNoTracking()
                .OrderBy(row => row.DisplayName)
                .ThenBy(row => row.ProfileId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.Select(Map).ToList();
        }
    }

    public async Task RevokeAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            string id = profileId.ToString("D");
            EnrolledProfileRow? row = await context.EnrolledProfiles
                .FirstOrDefaultAsync(profile => profile.ProfileId == id, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return;
            }

            row.RevokedUtc ??= DateTimeOffset.UtcNow;
            row.EnrollmentToken = null;
            row.EnrollmentTokenExpiresUtc = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<EnrolledProfile?> RedeemTokenAsync(
        Guid profileId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        string normalizedToken = RequireTrimmed(token, nameof(token));
        string tokenHash = HashToken(normalizedToken);
        string profileKey = profileId.ToString("D");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            EnrolledProfileRow? row = await context.EnrolledProfiles
                .FirstOrDefaultAsync(profile => profile.ProfileId == profileKey, cancellationToken)
                .ConfigureAwait(false);
            if (row is null ||
                row.RevokedUtc is not null ||
                row.EnrollmentTokenExpiresUtc is null ||
                row.EnrollmentTokenExpiresUtc <= now ||
                !string.Equals(row.EnrollmentToken, tokenHash, StringComparison.Ordinal))
            {
                return null;
            }

            row.EnrollmentToken = null;
            row.EnrollmentTokenExpiresUtc = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Map(row);
        }
    }

    internal static string HashToken(string token)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static EnrolledProfile Map(EnrolledProfileRow row) =>
        new(
            Guid.Parse(row.ProfileId),
            row.DisplayName,
            row.Role,
            row.RevokedUtc is null ? EnrollmentStatus.Active : EnrollmentStatus.Revoked,
            row.BirthYear,
            row.EnrolledUtc,
            row.RevokedUtc);

    private static string NormalizeRole(string? role)
    {
        string normalized = RequireTrimmed(role, nameof(role)).ToLowerInvariant();
        if (!EnrollableRoles.Contains(normalized))
        {
            throw new ArgumentException("Role must be student or teacher.", nameof(role));
        }

        return normalized;
    }

    private static string RequireTrimmed(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
