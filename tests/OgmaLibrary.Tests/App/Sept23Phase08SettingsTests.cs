using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Navigation;
using OgmaLibrary.App.Settings;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Metadata;

namespace OgmaLibrary.Tests.App;

/// <summary>
/// Sept-23 Phase 08 (K16): preference persistence and migration (8.1), the capability
/// resolution matrix (8.2), runtime gating of online metadata (8.3) and culture persistence (8.4).
/// </summary>
public sealed class Sept23Phase08SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogma-p08-{Guid.NewGuid():N}");

    public Sept23Phase08SettingsTests() => Directory.CreateDirectory(_directory);

    private string PreferencesPath => Path.Combine(_directory, "user-preferences.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // ── 8.1 Preferences ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Preferences_EveryField_RoundTripsAtTheCurrentSchema()
    {
        using var store = new FileUserPreferencesService(_directory);
        var saved = new UserPreferences(
            UserTheme.System,
            UserDensity.Compact,
            Culture: "fr",
            EnableMetadataProviders: true,
            EnableThreeDimensionalShelf: true,
            EnableClassroomHost: true);

        await store.SaveAsync(saved);

        Assert.Equal(saved, await store.GetAsync());
        Assert.Equal(UserPreferences.CurrentSchemaVersion, (await store.GetAsync()).SchemaVersion);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Preferences_SchemaOneFileAndUnknownFields_MigrateWithSafeDefaults()
    {
        // The Phase 18 (schema 1) file had only theme and density; a newer build may add fields.
        await File.WriteAllTextAsync(PreferencesPath, "{\"theme\":1,\"density\":1,\"futureSetting\":{\"x\":2}}");
        using var store = new FileUserPreferencesService(_directory);

        UserPreferences loaded = await store.GetAsync();

        Assert.Equal(UserTheme.Dark, loaded.Theme);
        Assert.Equal(UserDensity.Compact, loaded.Density);
        Assert.Null(loaded.Culture);
        Assert.False(loaded.EnableMetadataProviders);
        Assert.False(loaded.EnableThreeDimensionalShelf);
        Assert.False(loaded.EnableClassroomHost);
        Assert.Equal(UserPreferences.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.False(store.LastLoadWasRecovered);
    }

    [Theory]
    [InlineData("fr-CA", "fr")]
    [InlineData("EN", "en")]
    [InlineData("de", null)]
    [InlineData("", null)]
    public async Task Preferences_Culture_IsNormalisedToASupportedLanguageOrSystem(string stored, string? expected)
    {
        await File.WriteAllTextAsync(PreferencesPath, JsonSerializer.Serialize(new { culture = stored }));
        using var store = new FileUserPreferencesService(_directory);

        Assert.Equal(expected, (await store.GetAsync()).Culture);
    }

    [Fact]
    public async Task Preferences_CorruptFile_FallsBackToDefaultsKeepsACopyAndReportsIt()
    {
        await File.WriteAllTextAsync(PreferencesPath, "{ \"theme\": 1, broken");
        using var store = new FileUserPreferencesService(_directory);

        Assert.Equal(new UserPreferences(), await store.GetAsync());
        Assert.True(store.LastLoadWasRecovered);
        Assert.Equal("{ \"theme\": 1, broken", await File.ReadAllTextAsync(PreferencesPath + ".corrupt"));

        await store.SaveAsync(new UserPreferences(UserTheme.Dark));
        Assert.Equal(UserTheme.Dark, (await store.GetAsync()).Theme);
        Assert.False(store.LastLoadWasRecovered);
    }

    // ── 8.2 Capability resolution ───────────────────────────────────────────────

    public static TheoryData<bool?, bool, bool, bool> ResolutionMatrix => new()
    {
        // environment override, user preference, effective, managed by environment
        { null, false, false, false },
        { null, true, true, false },
        { true, false, true, true },
        { true, true, true, true },
        { false, false, false, true },
        { false, true, false, true },
    };

    [Theory]
    [MemberData(nameof(ResolutionMatrix))]
    public void Capabilities_EnvironmentOverrideWinsOverPreference_ForEveryCapability(
        bool? environment,
        bool preference,
        bool effective,
        bool managed)
    {
        foreach (UserCapability capability in Enum.GetValues<UserCapability>())
        {
            var overrides = capability switch
            {
                UserCapability.MetadataProviders => new CapabilityOverrides(MetadataProviders: environment),
                UserCapability.ThreeDimensionalShelf => new CapabilityOverrides(ThreeDimensionalShelf: environment),
                _ => new CapabilityOverrides(ClassroomHost: environment),
            };
            var state = new RuntimeCapabilityState(overrides: overrides);
            state.ApplyPreferences(new UserPreferences(
                EnableMetadataProviders: preference,
                EnableThreeDimensionalShelf: preference,
                EnableClassroomHost: preference));

            Assert.Equal(effective, state.IsEnabled(capability));
            Assert.Equal(managed, state.IsManagedByEnvironment(capability));
        }
    }

    [Fact]
    public void Capabilities_PreferenceChange_RaisesChangedOnlyWhenAnEffectiveValueChanges()
    {
        var state = new RuntimeCapabilityState(overrides: new CapabilityOverrides(ThreeDimensionalShelf: false));
        int changes = 0;
        state.Changed += (_, _) => changes++;

        state.ApplyPreferences(new UserPreferences(EnableThreeDimensionalShelf: true));
        Assert.Equal(0, changes);
        Assert.False(state.IsShelf3DAvailable);

        state.ApplyPreferences(new UserPreferences(EnableClassroomHost: true));
        Assert.Equal(1, changes);
        Assert.True(state.IsClassroomHostEnabled);
        Assert.True(((ICapabilityState)state).IsClassroomAvailable);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    public void RuntimeOptions_CapabilityVariables_AreOverridesAndNullWhenUnset(string? value, bool? expected)
    {
        OgmaRuntimeOptions options = OgmaRuntimeOptions.FromEnvironment(key => key switch
        {
            "OGMA_LIBRARY_DATA_DIR" => _directory,
            "OGMA_ENABLE_METADATA_PROVIDERS" or "OGMA_ENABLE_3D_SHELF" or "OGMA_ENABLE_CLASSROOM_HOST" => value,
            _ => null,
        });

        Assert.Equal(expected, options.EnableExternalMetadataProviders);
        Assert.Equal(expected, options.EnableThreeDimensionalShelf);
        Assert.Equal(expected, options.EnableClassroomHost);
    }

    // ── 8.3 Online metadata is gated at call time ───────────────────────────────

    [Fact]
    public async Task Metadata_PolicyOff_MakesNoProviderCall_AndTurningItOnNeedsNoRestart()
    {
        var provider = new CountingProvider();
        var state = new RuntimeCapabilityState();
        var aggregator = new MetadataProviderAggregator(
            [provider],
            new UnusedContextFactory(),
            policy: state);
        var gateway = new MetadataProviderGateway([provider], new UnusedContextFactory(), new MetadataProviderHealth(), state);
        var request = new MetadataLookupRequest("9780306406157", "Title", "Author");

        Assert.Empty(await aggregator.AggregateAsync("book-1", request));
        Assert.Empty(await gateway.SearchAsync(request));
        Assert.Equal(0, provider.Calls);

        // Turning the Settings switch on applies to the next lookup without a restart.
        state.ApplyPreferences(new UserPreferences(EnableMetadataProviders: true));
        Assert.True(((IMetadataProviderPolicy)state).AreOnlineProvidersEnabled);
    }

    [Fact]
    public void Composition_Default_RegistersProviderAdaptersBehindAClosedGate()
    {
        ServiceCollection descriptors = [];
        descriptors.AddOgmaLibrary(new OgmaRuntimeOptions { DataDirectory = _directory, LibraryRoot = _directory });

        using ServiceProvider services = descriptors.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Equal(2, services.GetServices<IMetadataProvider>().Count());
        Assert.False(services.GetRequiredService<IMetadataProviderPolicy>().AreOnlineProvidersEnabled);
        ICapabilitySettings settings = services.GetRequiredService<ICapabilitySettings>();
        Assert.Same(settings, services.GetRequiredService<ICapabilityState>());
        foreach (UserCapability capability in Enum.GetValues<UserCapability>())
        {
            Assert.False(settings.IsEnabled(capability));
            Assert.False(settings.IsManagedByEnvironment(capability));
        }
    }

    [Fact]
    public async Task Composition_EnvironmentOverride_IsManagedAndNotChangedByPreferences()
    {
        ServiceCollection descriptors = [];
        descriptors.AddOgmaLibrary(new OgmaRuntimeOptions
        {
            DataDirectory = _directory,
            LibraryRoot = _directory,
            EnableThreeDimensionalShelf = true,
        });
        using ServiceProvider services = descriptors.BuildServiceProvider();
        UserPreferencesController preferences = services.GetRequiredService<UserPreferencesController>();
        ICapabilitySettings settings = services.GetRequiredService<ICapabilitySettings>();

        await preferences.UpdateAsync(current => current with { EnableThreeDimensionalShelf = false });

        Assert.True(settings.IsManagedByEnvironment(UserCapability.ThreeDimensionalShelf));
        Assert.True(settings.IsEnabled(UserCapability.ThreeDimensionalShelf));
        Assert.Equal("OGMA_ENABLE_3D_SHELF", settings.EnvironmentVariableName(UserCapability.ThreeDimensionalShelf));
    }

    // ── 8.4 Culture ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fr-FR", "fr")]
    [InlineData("en-GB", "en")]
    [InlineData("sw-KE", "en")]
    public async Task Culture_FirstRun_FollowsTheSupportedOsLanguage(string osCulture, string expected)
    {
        var localization = new InMemoryLocalizationService();
        using var store = new FileUserPreferencesService(_directory);
        var controller = new UserPreferencesController(store, null, localization, () => CultureInfo.GetCultureInfo(osCulture));

        await controller.LoadAsync();

        Assert.Equal(expected, localization.CurrentCulture.TwoLetterISOLanguageName);
        Assert.Null(controller.Current.Culture);
    }

    [Fact]
    public async Task Culture_ChosenLanguage_AppliesLiveAndPersistsAcrossRestart()
    {
        var localization = new InMemoryLocalizationService();
        using (var store = new FileUserPreferencesService(_directory))
        {
            var controller = new UserPreferencesController(store, null, localization, () => CultureInfo.GetCultureInfo("en-US"));
            await controller.LoadAsync();
            int changed = 0;
            localization.CultureChanged += (_, _) => changed++;

            await controller.UpdateAsync(current => current with { Culture = "fr" });

            Assert.Equal("fr", localization.CurrentCulture.TwoLetterISOLanguageName);
            Assert.Equal(1, changed);
        }

        // "Restart": a fresh store, controller and localization service.
        var restarted = new InMemoryLocalizationService();
        using var reopened = new FileUserPreferencesService(_directory);
        var next = new UserPreferencesController(reopened, null, restarted, () => CultureInfo.GetCultureInfo("en-US"));
        await next.LoadAsync();
        Assert.Equal("fr", next.Current.Culture);
        Assert.Equal("fr", restarted.CurrentCulture.TwoLetterISOLanguageName);
    }

    private sealed class CountingProvider : IMetadataProvider
    {
        public int Calls { get; private set; }

        public string ProviderName => "Counting";

        public Task<ProviderMetadataResult?> LookupAsync(string isbn13, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<ProviderMetadataResult?>(null);
        }
    }

    private sealed class UnusedContextFactory : IDbContextFactory<CatalogueDbContext>
    {
        public CatalogueDbContext CreateDbContext() =>
            throw new InvalidOperationException("A gated lookup must not open the catalogue.");
    }
}
