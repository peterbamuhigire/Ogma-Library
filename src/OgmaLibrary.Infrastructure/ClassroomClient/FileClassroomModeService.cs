using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>File-backed runtime mode settings for Standalone versus Client mode.</summary>
internal sealed class FileClassroomModeService : IClassroomModeService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly string _syncSettingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ObservableEvents<ClassroomConnectivityStatus> _connectivity = new();
    private ClassroomConnectivityStatus _connectivityStatus = new(
        IsOnline: false,
        UpdatedUtc: DateTimeOffset.MinValue,
        Message: "Not connected");

    public FileClassroomModeService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "classroom", "mode.json");
        _syncSettingsPath = Path.Combine(dataDirectory, "classroom", "sync.json");
    }

    public IObservable<ClassroomConnectivityStatus> Connectivity => _connectivity;

    public async Task<ClassroomModeSettings> GetModeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new ClassroomModeSettings(LibraryRuntimeMode.Standalone);
            }

            using FileStream stream = File.OpenRead(_settingsPath);
            ClassroomModeSettings? settings = await JsonSerializer
                .DeserializeAsync<ClassroomModeSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return settings ?? new ClassroomModeSettings(LibraryRuntimeMode.Standalone);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveModeAsync(ClassroomModeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Mode))
        {
            throw new ArgumentException("Unsupported classroom mode.", nameof(settings));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string tempPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (FileStream stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
                }

                File.Move(tempPath, _settingsPath, overwrite: true);
            }
            finally
            {
                DeleteTemporaryFile(tempPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ClassroomSyncSettings> GetSyncSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_syncSettingsPath))
            {
                return new ClassroomSyncSettings();
            }

            using FileStream stream = File.OpenRead(_syncSettingsPath);
            ClassroomSyncSettings? settings = await JsonSerializer
                .DeserializeAsync<ClassroomSyncSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return Normalize(settings ?? new ClassroomSyncSettings());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveSyncSettingsAsync(
        ClassroomSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ClassroomSyncSettings normalized = Normalize(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_syncSettingsPath)!);
            string tempPath = $"{_syncSettingsPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (FileStream stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                }

                File.Move(tempPath, _syncSettingsPath, overwrite: true);
            }
            finally
            {
                DeleteTemporaryFile(tempPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<ClassroomConnectivityStatus> GetConnectivityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_connectivityStatus);
    }

    public Task SetConnectivityAsync(
        ClassroomConnectivityStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);
        cancellationToken.ThrowIfCancellationRequested();
        _connectivityStatus = status;
        _connectivity.Publish(status);
        return Task.CompletedTask;
    }

    public void Dispose() => _gate.Dispose();

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static ClassroomSyncSettings Normalize(ClassroomSyncSettings settings) =>
        settings.IsEnabled ? settings : settings with { SyncOnReconnect = false };
}
