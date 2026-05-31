using System.Text.Json;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Persists library root and excluded-folder settings to a JSON file in the
/// application data directory (FR-LIB-001). Thread-safe via a
/// <see cref="SemaphoreSlim"/> so concurrent read/write from background tasks is safe.
/// </summary>
public sealed class LibrarySettingsService : ILibrarySettingsService, IDisposable
{
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Serialization DTO — internal only, not part of the public contract.
    private sealed class SettingsDto
    {
        public string? LibraryRoot { get; set; }
        public List<string> ExcludedFolders { get; set; } = [];
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LibrarySettingsService"/>.
    /// </summary>
    /// <param name="dataDirectory">
    /// The directory under which <c>library-settings.json</c> is stored.
    /// The directory is created if it does not exist.
    /// </param>
    public LibrarySettingsService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "library-settings.json");
    }

    /// <inheritdoc />
    public async Task<string?> GetLibraryRootAsync(CancellationToken cancellationToken = default)
    {
        var dto = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return dto.LibraryRoot;
    }

    /// <inheritdoc />
    public async Task SetLibraryRootAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            dto.LibraryRoot = rootPath;
            await SaveLockedAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetExcludedFoldersAsync(CancellationToken cancellationToken = default)
    {
        var dto = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return dto.ExcludedFolders.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task SetExcludedFoldersAsync(
        IReadOnlyList<string> excludedFolders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excludedFolders);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            dto.ExcludedFolders = [.. excludedFolders];
            await SaveLockedAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();

    private async Task<SettingsDto> LoadAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<SettingsDto> LoadLockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new SettingsDto();
        }

        var stream = File.OpenRead(_settingsPath);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer
                .DeserializeAsync<SettingsDto>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? new SettingsDto();
        }
    }

    private async Task SaveLockedAsync(SettingsDto dto, CancellationToken cancellationToken)
    {
        var stream = File.Open(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(stream, dto, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
