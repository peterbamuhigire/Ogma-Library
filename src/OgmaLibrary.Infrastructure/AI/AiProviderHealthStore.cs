using System.Text.Json;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Durable store for redacted AI provider health counters.</summary>
public interface IAiProviderHealthStore
{
    /// <summary>Loads previously persisted provider health snapshots.</summary>
    IReadOnlyList<AiProviderHealthSnapshot> Load();

    /// <summary>Atomically persists the supplied provider health snapshots.</summary>
    void Save(IReadOnlyCollection<AiProviderHealthSnapshot> snapshots);
}

/// <summary>
/// JSON file store for provider health. It contains operational counters only;
/// secrets, prompts, responses, and endpoint values are intentionally absent.
/// </summary>
public sealed class JsonAiProviderHealthStore : IAiProviderHealthStore
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly string _path;

    /// <summary>Initializes a store at the supplied application-data path.</summary>
    public JsonAiProviderHealthStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <inheritdoc />
    public IReadOnlyList<AiProviderHealthSnapshot> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            HealthDocument? document = JsonSerializer.Deserialize<HealthDocument>(
                File.ReadAllText(_path),
                JsonOptions);
            return document?.Version == CurrentVersion
                ? document.Providers
                    .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.ProviderKey))
                    .Select(snapshot => snapshot with { ProviderKey = snapshot.ProviderKey.Trim() })
                    .ToArray()
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public void Save(IReadOnlyCollection<AiProviderHealthSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var document = new HealthDocument(CurrentVersion, snapshots.ToList());
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record HealthDocument(
        int Version,
        IReadOnlyList<AiProviderHealthSnapshot> Providers);
}
