using OgmaLibrary.Application;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 Phase 14 slice 3: the semantic provider preference (schema 4) round-trips,
/// and files written before it read as the built-in provider.
/// </summary>
public sealed class UserPreferencesSemanticProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogma-p14p-{Guid.NewGuid():N}");

    public UserPreferencesSemanticProviderTests() => Directory.CreateDirectory(_directory);

    private string PreferencesPath => Path.Combine(_directory, "user-preferences.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Defaults_AreSchemaFourAndBuiltIn()
    {
        Assert.Equal(4, UserPreferences.CurrentSchemaVersion);
        Assert.Equal(SemanticProviderChoice.BuiltIn, new UserPreferences().SemanticProvider);
    }

    [Theory]
    [InlineData(SemanticProviderChoice.BuiltIn)]
    [InlineData(SemanticProviderChoice.Ollama)]
    [InlineData(SemanticProviderChoice.Off)]
    public async Task SemanticProvider_RoundTrips(SemanticProviderChoice choice)
    {
        using var store = new FileUserPreferencesService(_directory);

        await store.SaveAsync(new UserPreferences(Culture: "fr", SemanticProvider: choice));

        using var reopened = new FileUserPreferencesService(_directory);
        UserPreferences loaded = await reopened.GetAsync();
        Assert.Equal(choice, loaded.SemanticProvider);
        Assert.Equal("fr", loaded.Culture);
        Assert.Equal(4, loaded.SchemaVersion);
    }

    [Fact]
    public async Task SchemaThreeFile_WithoutTheField_ReadsAsBuiltIn()
    {
        await File.WriteAllTextAsync(
            PreferencesPath,
            "{\"theme\":1,\"culture\":\"fr\",\"enableMetadataProviders\":true,\"inspectorWidth\":400,\"schemaVersion\":3}");
        using var store = new FileUserPreferencesService(_directory);

        UserPreferences loaded = await store.GetAsync();

        Assert.Equal(SemanticProviderChoice.BuiltIn, loaded.SemanticProvider);
        Assert.Equal(UserTheme.Dark, loaded.Theme);
        Assert.Equal(400, loaded.InspectorWidth);
        Assert.True(loaded.EnableMetadataProviders);
        Assert.Equal(4, loaded.SchemaVersion);
        Assert.False(store.LastLoadWasRecovered);
    }

    [Fact]
    public async Task UnknownProviderValue_FallsBackToBuiltIn()
    {
        await File.WriteAllTextAsync(PreferencesPath, "{\"semanticProvider\":42,\"schemaVersion\":4}");
        using var store = new FileUserPreferencesService(_directory);

        UserPreferences loaded = await store.GetAsync();

        Assert.Equal(SemanticProviderChoice.BuiltIn, loaded.SemanticProvider);
    }
}
