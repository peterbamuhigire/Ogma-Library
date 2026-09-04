using System.Text.Json;
using OgmaLibrary.Application.Catalogue;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>JSON-backed catalogue presentation state store.</summary>
public sealed class FileCatalogueViewStateStore : ICatalogueViewStateStore, IDisposable
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Creates a store below the application data directory.</summary>
    public FileCatalogueViewStateStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "catalogue-view-state.json");
    }

    /// <inheritdoc />
    public async Task<CatalogueViewState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            FileStream stream = File.OpenRead(_path);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<CatalogueViewState>(
                    stream,
                    Options,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            // A corrupt preference file must never prevent the catalogue from opening.
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        CatalogueViewState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            FileStream stream = File.Create(temporaryPath);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(stream, state, Options, cancellationToken)
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

            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();
}
