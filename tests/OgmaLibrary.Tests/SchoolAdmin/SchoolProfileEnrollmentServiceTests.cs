using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 data-backed school profile enrollment tests.</summary>
public sealed class SchoolProfileEnrollmentServiceTests
{
    [Fact]
    public async Task ProfileEnrollmentService_EnrollListAndRevoke_RoundTripsStatus()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();

            EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
                "Amina Reader",
                "Student",
                BirthYear: 2014));
            IReadOnlyList<EnrolledProfile> activeProfiles = await enrollment.ListAsync();
            await enrollment.RevokeAsync(token.ProfileId);
            IReadOnlyList<EnrolledProfile> revokedProfiles = await enrollment.ListAsync();

            Assert.NotEqual(Guid.Empty, token.ProfileId);
            Assert.Equal(64, token.Token.Length);
            Assert.True(token.ExpiresUtc > DateTimeOffset.UtcNow);
            EnrolledProfile active = Assert.Single(activeProfiles);
            Assert.Equal(token.ProfileId, active.ProfileId);
            Assert.Equal("Amina Reader", active.DisplayName);
            Assert.Equal("student", active.Role);
            Assert.Equal(EnrollmentStatus.Active, active.Status);
            Assert.Equal(2014, active.BirthYear);
            EnrolledProfile revoked = Assert.Single(revokedProfiles);
            Assert.Equal(EnrollmentStatus.Revoked, revoked.Status);
            Assert.NotNull(revoked.RevokedUtc);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileEnrollmentService_StoresTokenHashAndDefaultEntitlement()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();

            EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
                "Teacher Okello",
                "Teacher",
                BirthYear: null));

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            var row = await context.EnrolledProfiles.SingleAsync();
            var entitlement = await context.SchoolAiEntitlements.SingleAsync();

            Assert.NotEqual(token.Token, row.EnrollmentToken);
            Assert.Equal(SchoolProfileEnrollmentService.HashToken(token.Token), row.EnrollmentToken);
            Assert.Equal(token.ProfileId.ToString("D"), entitlement.ProfileId);
            Assert.Equal(10_000, entitlement.DailyTokenBudget);
            Assert.Equal(500_000, entitlement.ClassDailyTokenBudget);
            Assert.Equal(5, entitlement.RateLimitQueriesPerMin);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileEnrollmentService_RejectsAdminEnrollmentRole()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();

            await Assert.ThrowsAsync<ArgumentException>(() => enrollment.EnrollAsync(new EnrollProfileRequest(
                "Local Admin",
                "admin",
                BirthYear: null)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileEnrollmentService_RedeemToken_ConsumesOneTimeToken()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();
            EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
                "Amina Reader",
                "Student",
                BirthYear: 2014));

            EnrolledProfile? redeemed = await enrollment.RedeemTokenAsync(token.ProfileId, token.Token);
            EnrolledProfile? replay = await enrollment.RedeemTokenAsync(token.ProfileId, token.Token);

            await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
            var row = await context.EnrolledProfiles.SingleAsync();
            Assert.NotNull(redeemed);
            Assert.Equal(token.ProfileId, redeemed.ProfileId);
            Assert.Equal("student", redeemed.Role);
            Assert.Null(replay);
            Assert.Null(row.EnrollmentToken);
            Assert.Null(row.EnrollmentTokenExpiresUtc);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileEnrollmentService_RedeemToken_RejectsRevokedProfile()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();
            EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
                "Amina Reader",
                "Student",
                BirthYear: 2014));
            await enrollment.RevokeAsync(token.ProfileId);

            EnrolledProfile? redeemed = await enrollment.RedeemTokenAsync(token.ProfileId, token.Token);

            Assert.Null(redeemed);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileEnrollmentService_Revoke_RevokesOnlyMatchingOutstandingSessions()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            await using ServiceProvider provider = await CreateServicesAsync(dataDirectory);
            var enrollment = provider.GetRequiredService<IProfileEnrollmentService>();
            EnrollmentToken token = await enrollment.EnrollAsync(new EnrollProfileRequest(
                "Amina Reader",
                "Student",
                BirthYear: 2014));
            await using (CatalogueDbContext seed = provider.GetRequiredService<CatalogueDbContext>())
            {
                seed.HostClientSessions.AddRange(
                    new HostClientSessionRow
                    {
                        TokenHash = new string('a', 64),
                        ClientId = token.ProfileId.ToString("D"),
                        Role = "student",
                        IssuedUtc = DateTimeOffset.UtcNow,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                    },
                    new HostClientSessionRow
                    {
                        TokenHash = new string('b', 64),
                        ClientId = Guid.NewGuid().ToString("D"),
                        Role = "student",
                        IssuedUtc = DateTimeOffset.UtcNow,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                    });
                await seed.SaveChangesAsync();
            }

            await enrollment.RevokeAsync(token.ProfileId);

            await using CatalogueDbContext verify = provider.GetRequiredService<CatalogueDbContext>();
            EnrolledProfileRow profile = await verify.EnrolledProfiles.SingleAsync();
            HostClientSessionRow matching = await verify.HostClientSessions.SingleAsync(
                session => session.ClientId == token.ProfileId.ToString("D"));
            HostClientSessionRow unrelated = await verify.HostClientSessions.SingleAsync(
                session => session.ClientId != token.ProfileId.ToString("D"));
            Assert.NotNull(profile.RevokedUtc);
            Assert.Equal(profile.RevokedUtc, matching.RevokedUtc);
            Assert.Null(unrelated.RevokedUtc);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddSchoolAdminServices(dataDirectory)
            .BuildServiceProvider();
        await using CatalogueDbContext context = provider.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return provider;
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-school-admin-enrollment-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
