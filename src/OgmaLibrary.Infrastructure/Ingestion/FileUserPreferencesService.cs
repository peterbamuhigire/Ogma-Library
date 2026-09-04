using System.Text.Json;
using OgmaLibrary.Application;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Atomic JSON persistence for user-facing appearance preferences.</summary>
public sealed class FileUserPreferencesService : IUserPreferencesService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a preference store beneath the supplied app-data directory.</summary>
    public FileUserPreferencesService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "user-preferences.json");
    }

    /// <inheritdoc />
    public async Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return new UserPreferences();
            }

            FileStream stream = File.OpenRead(_path);
            await using (stream.ConfigureAwait(false))
            {
                UserPreferences? preferences = await JsonSerializer
                    .DeserializeAsync<UserPreferences>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                return Normalize(preferences);
            }
        }
        catch (JsonException)
        {
            // A corrupt preference file must never prevent the catalogue from opening.
            return new UserPreferences();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        UserPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        UserPreferences normalized = Normalize(preferences);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private static UserPreferences Normalize(UserPreferences? preferences)
    {
        if (preferences is null)
        {
            return new UserPreferences();
        }

        UserTheme theme = Enum.IsDefined(preferences.Theme) ? preferences.Theme : UserTheme.Light;
        UserDensity density = Enum.IsDefined(preferences.Density)
            ? preferences.Density
            : UserDensity.Comfortable;
        return new UserPreferences(theme, density);
    }
}
