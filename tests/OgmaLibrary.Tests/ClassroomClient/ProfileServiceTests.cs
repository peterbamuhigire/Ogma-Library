using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 profile-management tests.</summary>
public sealed class ProfileServiceTests
{
    [Fact]
    public async Task ProfileService_CreatesProfile_WithUuidV4_AndPersistsSelection()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider firstProvider = CreateProvider(dataDirectory);
            IProfileService firstService = firstProvider.GetRequiredService<IProfileService>();

            ClassroomProfile profile = await firstService.CreateAsync(
                new CreateClassroomProfileRequest("  Amina  ", ClassroomRole.Student));

            using ServiceProvider secondProvider = CreateProvider(dataDirectory);
            IProfileService secondService = secondProvider.GetRequiredService<IProfileService>();
            IReadOnlyList<ClassroomProfile> profiles = await secondService.ListAsync();
            ClassroomProfile? active = await secondService.GetActiveAsync();

            Assert.Equal('4', profile.ProfileId.ToString("D")[14]);
            Assert.Equal("Amina", profile.DisplayName);
            Assert.False(profile.IsGuest);
            Assert.Single(profiles);
            Assert.Equal(profile.ProfileId, profiles[0].ProfileId);
            Assert.Equal(profile.ProfileId, active!.ProfileId);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileService_GuestSession_WritesNoDbRow()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = CreateProvider(dataDirectory);
            IProfileService service = provider.GetRequiredService<IProfileService>();

            ClassroomProfile guest = await service.CreateGuestSessionAsync();
            IReadOnlyList<ClassroomProfile> profiles = await service.ListAsync();
            string profileRoot = Path.Combine(dataDirectory, "classroom", "profiles");

            Assert.True(guest.IsGuest);
            Assert.Equal(ClassroomRole.Guest, guest.Role);
            Assert.Empty(profiles);
            Assert.False(Directory.Exists(profileRoot));

            await service.ClearGuestSessionAsync();

            Assert.Null(await service.GetActiveAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileService_SessionToken_UsesCredentialStoreKey()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = CreateProvider(dataDirectory);
            IProfileService service = provider.GetRequiredService<IProfileService>();
            IClassroomCredentialStore credentialStore = provider.GetRequiredService<IClassroomCredentialStore>();
            ClassroomProfile profile = await service.CreateAsync(
                new CreateClassroomProfileRequest("Teacher Grace", ClassroomRole.Teacher));

            await service.StoreSessionTokenAsync(profile.ProfileId, "session-token");

            Assert.Equal("session-token", await service.GetSessionTokenAsync(profile.ProfileId));
            Assert.Equal(
                "session-token",
                await credentialStore.GetSecretAsync($"ogma.classroom.session.{profile.ProfileId:N}"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileService_DeleteProfile_ClearsCredentialStore_And_DbFile()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = CreateProvider(dataDirectory);
            IProfileService service = provider.GetRequiredService<IProfileService>();
            IStudentPrivateRepository privateRepository = provider.GetRequiredService<IStudentPrivateRepository>();
            ClassroomProfile profile = await service.CreateAsync(
                new CreateClassroomProfileRequest("Amina", ClassroomRole.Student));
            string privateDbPath = privateRepository.GetPrivateDatabasePath(profile.ProfileId);
            await service.StoreSessionTokenAsync(profile.ProfileId, "session-token");

            await service.DeleteAsync(profile.ProfileId);

            Assert.Empty(await service.ListAsync());
            Assert.Null(await service.GetActiveAsync());
            Assert.Null(await service.GetSessionTokenAsync(profile.ProfileId));
            Assert.False(Directory.Exists(Path.GetDirectoryName(privateDbPath)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static ServiceProvider CreateProvider(string dataDirectory) =>
        new ServiceCollection()
            .AddClassroomClientServices(dataDirectory)
            .BuildServiceProvider();

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-classroom-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
