using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Classroom client contract tests.</summary>
public sealed class ClassroomClientScaffoldTests
{
    [Fact]
    public async Task ClassroomMode_DefaultsToStandalone()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            IClassroomModeService service = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider()
                .GetRequiredService<IClassroomModeService>();

            ClassroomModeSettings settings = await service.GetModeAsync();

            Assert.Equal(LibraryRuntimeMode.Standalone, settings.Mode);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ProfileService_CreatesPersistentAndGuestProfilesSeparately()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var service = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider()
                .GetRequiredService<IProfileService>();

            ClassroomProfile student = await service.CreateAsync(
                new CreateClassroomProfileRequest("Amina", ClassroomRole.Student));
            ClassroomProfile guest = await service.CreateGuestSessionAsync();
            IReadOnlyList<ClassroomProfile> profiles = await service.ListAsync();

            Assert.False(student.IsGuest);
            Assert.Equal(ClassroomRole.Student, student.Role);
            Assert.True(guest.IsGuest);
            Assert.Equal(ClassroomRole.Guest, guest.Role);
            Assert.Single(profiles);
            Assert.Equal(student.ProfileId, profiles[0].ProfileId);
            Assert.Equal(guest.ProfileId, (await service.GetActiveAsync())!.ProfileId);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_UsesSeparatePerProfileDatabasePaths()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var repository = new StudentPrivateRepository(dataDirectory);
            Guid firstProfile = Guid.NewGuid();
            Guid secondProfile = Guid.NewGuid();

            string firstPath = repository.GetPrivateDatabasePath(firstProfile);
            string secondPath = repository.GetPrivateDatabasePath(secondProfile);
            await repository.EnsureCreatedAsync(firstProfile);

            Assert.NotEqual(firstPath, secondPath);
            Assert.EndsWith(Path.Combine("classroom", "profiles", firstProfile.ToString("N"), "private.db"), firstPath);
            Assert.True(Directory.Exists(Path.GetDirectoryName(firstPath)));
            Assert.False(Directory.Exists(Path.GetDirectoryName(secondPath)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task OfflineCache_IsScopedByHostAndResource()
    {
        var cache = new InMemoryOfflineCacheService();
        var first = new OfflineCacheEntry(
            "host-a",
            "books/1/page/1",
            "etag-a",
            [1, 2, 3],
            DateTimeOffset.UtcNow);
        var second = first with { HostId = "host-b", Content = [4, 5, 6] };

        await cache.PutAsync(first);
        await cache.PutAsync(second);

        Assert.Equal(first.Content, (await cache.GetAsync("host-a", "books/1/page/1"))!.Content);
        Assert.Equal(second.Content, (await cache.GetAsync("host-b", "books/1/page/1"))!.Content);

        await cache.ClearHostAsync("host-a");

        Assert.Null(await cache.GetAsync("host-a", "books/1/page/1"));
        Assert.NotNull(await cache.GetAsync("host-b", "books/1/page/1"));
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-classroom-{Guid.NewGuid():N}");
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
