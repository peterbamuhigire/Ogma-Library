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
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileClassroomModeService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "classroom", "mode.json");
    }

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
            using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
