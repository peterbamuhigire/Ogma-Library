using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 runtime mode persistence tests.</summary>
public sealed class ClassroomModeServiceTests
{
    [Fact]
    public async Task ClassroomMode_DefaultsToStandalone_WhenSettingsFileMissing()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = CreateProvider(dataDirectory);
            IClassroomModeService service = provider.GetRequiredService<IClassroomModeService>();

            ClassroomModeSettings settings = await service.GetModeAsync();

            Assert.Equal(LibraryRuntimeMode.Standalone, settings.Mode);
            Assert.False(File.Exists(Path.Combine(dataDirectory, "classroom", "mode.json")));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClassroomMode_PersistsAcrossProviderRestart()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using (ServiceProvider firstProvider = CreateProvider(dataDirectory))
            {
                IClassroomModeService firstService = firstProvider.GetRequiredService<IClassroomModeService>();
                await firstService.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
            }

            using ServiceProvider secondProvider = CreateProvider(dataDirectory);
            IClassroomModeService secondService = secondProvider.GetRequiredService<IClassroomModeService>();

            ClassroomModeSettings settings = await secondService.GetModeAsync();

            Assert.Equal(LibraryRuntimeMode.ConnectToHost, settings.Mode);
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
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-classroom-mode-{Guid.NewGuid():N}");
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
