using OgmaLibrary.App.Settings;
using OgmaLibrary.Application;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 Phase 11, task 11.9: the inspector width is a persisted preference (schema 3),
/// clamped to 320–560 on load and save, with schema 2 files migrating to the 360 default.
/// </summary>
public sealed class Sept23Phase11InspectorWidthTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogma-p11w-{Guid.NewGuid():N}");

    public Sept23Phase11InspectorWidthTests() => Directory.CreateDirectory(_directory);

    private string PreferencesPath => Path.Combine(_directory, "user-preferences.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Preferences_SchemaIsThree_AndTheDefaultWidthIs360()
    {
        Assert.Equal(3, UserPreferences.CurrentSchemaVersion);
        Assert.Equal(360, new UserPreferences().InspectorWidth);
        Assert.Equal(320, UserPreferences.MinInspectorWidth);
        Assert.Equal(560, UserPreferences.MaxInspectorWidth);
    }

    [Fact]
    public async Task Preferences_InspectorWidth_RoundTrips()
    {
        using var store = new FileUserPreferencesService(_directory);

        await store.SaveAsync(new UserPreferences(InspectorWidth: 448));

        UserPreferences loaded = await store.GetAsync();
        Assert.Equal(448, loaded.InspectorWidth);
        Assert.Equal(3, loaded.SchemaVersion);
    }

    [Fact]
    public async Task Preferences_SchemaTwoFile_MigratesToTheDefaultWidth()
    {
        await File.WriteAllTextAsync(
            PreferencesPath,
            "{\"theme\":1,\"density\":0,\"culture\":\"fr\",\"enableMetadataProviders\":true,\"schemaVersion\":2}");
        using var store = new FileUserPreferencesService(_directory);

        UserPreferences loaded = await store.GetAsync();

        Assert.Equal(UserTheme.Dark, loaded.Theme);
        Assert.Equal("fr", loaded.Culture);
        Assert.True(loaded.EnableMetadataProviders);
        Assert.Equal(360, loaded.InspectorWidth);
        Assert.Equal(3, loaded.SchemaVersion);
        Assert.False(store.LastLoadWasRecovered);
    }

    [Theory]
    [InlineData("100", 320)]
    [InlineData("0", 320)]
    [InlineData("-40", 320)]
    [InlineData("9000", 560)]
    [InlineData("320", 320)]
    [InlineData("560", 560)]
    [InlineData("401.5", 401.5)]
    public async Task Preferences_InspectorWidth_IsClampedOnLoad(string stored, double expected)
    {
        await File.WriteAllTextAsync(PreferencesPath, $"{{\"inspectorWidth\":{stored},\"schemaVersion\":3}}");
        using var store = new FileUserPreferencesService(_directory);

        Assert.Equal(expected, (await store.GetAsync()).InspectorWidth);
    }

    [Theory]
    [InlineData(double.NaN, 360)]
    [InlineData(double.PositiveInfinity, 360)]
    [InlineData(10, 320)]
    [InlineData(800, 560)]
    public void Preferences_Normalize_ClampsAndRejectsNonFiniteWidths(double width, double expected) =>
        Assert.Equal(expected, FileUserPreferencesService.Normalize(new UserPreferences(InspectorWidth: width)).InspectorWidth);

    [Theory]
    [InlineData(10, 320)]
    [InlineData(800, 560)]
    [InlineData(double.NaN, 360)]
    [InlineData(400, 400)]
    public void ClampInspectorWidth_FollowsTheSameRule(double width, double expected) =>
        Assert.Equal(expected, UserPreferences.ClampInspectorWidth(width));

    [Fact]
    public async Task Controller_WidthChange_PersistsAndSurvivesARestart()
    {
        using (var store = new FileUserPreferencesService(_directory))
        using (var controller = new UserPreferencesController(store))
        {
            await controller.LoadAsync();
            await controller.UpdateAsync(current => current with { InspectorWidth = 512 });
        }

        using var reopened = new FileUserPreferencesService(_directory);
        using var restarted = new UserPreferencesController(reopened);
        await restarted.LoadAsync();
        Assert.Equal(512, restarted.Current.InspectorWidth);
    }
}
