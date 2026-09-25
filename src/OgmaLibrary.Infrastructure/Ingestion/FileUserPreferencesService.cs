using System.Globalization;
using System.Text.Json;
using OgmaLibrary.Application;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Atomic JSON persistence for user preferences (Sept-23 Phase 08). Writes go to a temporary
/// file that replaces the target in one move; an unreadable file is kept as
/// <c>user-preferences.json.corrupt</c> and defaults are returned, so a damaged file never
/// prevents the catalogue from opening. Unknown fields are ignored and older schema versions
/// are migrated on read.
/// </summary>
public sealed class FileUserPreferencesService : IUserPreferencesService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly string[] SupportedCultures = ["en", "fr"];

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
    public bool LastLoadWasRecovered { get; private set; }

    /// <inheritdoc />
    public async Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastLoadWasRecovered = false;
            if (!File.Exists(_path))
            {
                return new UserPreferences();
            }

            try
            {
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
                // Keep the damaged copy for support and report the recovery to the shell.
                Quarantine();
                LastLoadWasRecovered = true;
                return new UserPreferences();
            }
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

    /// <summary>Validates every field and migrates the schema to the current version.</summary>
    /// <param name="preferences">The deserialised preferences, or null.</param>
    /// <returns>Safe preferences at <see cref="UserPreferences.CurrentSchemaVersion"/>.</returns>
    public static UserPreferences Normalize(UserPreferences? preferences)
    {
        if (preferences is null)
        {
            return new UserPreferences();
        }

        UserTheme theme = Enum.IsDefined(preferences.Theme) ? preferences.Theme : UserTheme.Light;
        UserDensity density = Enum.IsDefined(preferences.Density)
            ? preferences.Density
            : UserDensity.Comfortable;
        return preferences with
        {
            Theme = theme,
            Density = density,
            Culture = NormalizeCulture(preferences.Culture),
            SchemaVersion = UserPreferences.CurrentSchemaVersion,
        };
    }

    private static string? NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        string candidate = culture.Trim();
        int separator = candidate.IndexOfAny(['-', '_']);
        string language = (separator > 0 ? candidate[..separator] : candidate)
            .ToLower(CultureInfo.InvariantCulture);
        return Array.IndexOf(SupportedCultures, language) >= 0 ? language : null;
    }

    private void Quarantine()
    {
        try
        {
            File.Copy(_path, _path + ".corrupt", overwrite: true);
        }
        catch (IOException)
        {
            // Intentionally ignored: the copy is a support aid; defaults are still returned.
        }
        catch (UnauthorizedAccessException)
        {
            // Intentionally ignored: as above, a read-only folder must not block start-up.
        }
    }
}
