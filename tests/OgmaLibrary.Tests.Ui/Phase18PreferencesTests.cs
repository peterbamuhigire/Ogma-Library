using System.Text.Json;
using Avalonia.Headless.XUnit;
using OgmaLibrary.Application;
using OgmaLibrary.Infrastructure.Ingestion;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for Phase 18 appearance persistence and command discovery.</summary>
public sealed class Phase18PreferencesTests
{
    [AvaloniaFact]
    public async Task Preferences_RoundTripAndRecoverFromCorruptFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            using var store = new FileUserPreferencesService(directory);

            Assert.Equal(new UserPreferences(), await store.GetAsync());
            await store.SaveAsync(new UserPreferences(UserTheme.Dark, UserDensity.Compact));
            Assert.Equal(
                new UserPreferences(UserTheme.Dark, UserDensity.Compact),
                await store.GetAsync());

            await File.WriteAllTextAsync(
                Path.Combine(directory, "user-preferences.json"),
                "{ not valid json");
            Assert.Equal(new UserPreferences(), await store.GetAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Preferences_InvalidEnumValuesFailClosed()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            using var store = new FileUserPreferencesService(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "user-preferences.json"),
                JsonSerializer.Serialize(new { Theme = 99, Density = 99 }));

            Assert.Equal(new UserPreferences(), await store.GetAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-phase18-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
