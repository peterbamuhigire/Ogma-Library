using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Disk-backed LRU cache for Host-served classroom resources.</summary>
internal sealed class DiskOfflineCacheService : IOfflineCacheService, IDisposable
{
    private const long DefaultSizeLimitBytes = 500L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _cacheRoot;
    private readonly long _sizeLimitBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DiskOfflineCacheService(string dataDirectory, long sizeLimitBytes = DefaultSizeLimitBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        if (sizeLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeLimitBytes), "Cache size limit must be positive.");
        }

        _cacheRoot = Path.Combine(dataDirectory, "classroom", "cache");
        _sizeLimitBytes = sizeLimitBytes;
    }

    public async Task<OfflineCacheEntry?> GetAsync(
        string hostId,
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(hostId, resourceKey);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string metadataPath = GetMetadataPath(hostId, resourceKey);
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            CacheMetadata? metadata = await ReadMetadataAsync(metadataPath, cancellationToken).ConfigureAwait(false);
            string expectedCacheKey = CreateCacheKey(hostId, resourceKey);
            if (metadata is null ||
                !string.Equals(metadata.HostId, hostId, StringComparison.Ordinal) ||
                !string.Equals(metadata.ResourceKey, resourceKey, StringComparison.Ordinal) ||
                !string.Equals(metadata.ContentFile, $"{expectedCacheKey}.bin", StringComparison.Ordinal) ||
                !File.Exists(GetContentPath(metadata)))
            {
                DeleteEntryFiles(metadataPath, metadata);
                return null;
            }

            byte[] content = await File.ReadAllBytesAsync(GetContentPath(metadata), cancellationToken)
                .ConfigureAwait(false);
            if (metadata.ContentLength < 0 || metadata.ContentLength != content.LongLength)
            {
                DeleteEntryFiles(metadataPath, metadata);
                return null;
            }

            string contentHash = Convert.ToHexStringLower(SHA256.HashData(content));
            if (!string.Equals(metadata.ContentHash, contentHash, StringComparison.Ordinal))
            {
                DeleteEntryFiles(metadataPath, metadata);
                return null;
            }

            metadata.LastAccessUtc = DateTimeOffset.UtcNow;
            metadata.ContentLength = content.LongLength;
            await WriteMetadataAsync(metadataPath, metadata, cancellationToken).ConfigureAwait(false);

            return new OfflineCacheEntry(
                metadata.HostId,
                metadata.ResourceKey,
                metadata.ETag,
                content,
                metadata.StoredUtc,
                metadata.ContentType);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PutAsync(OfflineCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateKey(entry.HostId, entry.ResourceKey);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_cacheRoot);
            string cacheKey = CreateCacheKey(entry.HostId, entry.ResourceKey);
            string contentPath = Path.Combine(_cacheRoot, $"{cacheKey}.bin");
            string metadataPath = Path.Combine(_cacheRoot, $"{cacheKey}.json");
            var metadata = new CacheMetadata
            {
                HostId = entry.HostId,
                ResourceKey = entry.ResourceKey,
                ETag = entry.ETag,
                StoredUtc = entry.StoredUtc,
                LastAccessUtc = DateTimeOffset.UtcNow,
                ContentLength = entry.Content.LongLength,
                ContentHash = Convert.ToHexStringLower(SHA256.HashData(entry.Content)),
                ContentType = entry.ContentType,
                ContentFile = Path.GetFileName(contentPath),
            };

            string temporaryContentPath = $"{contentPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryContentPath, entry.Content, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryContentPath, contentPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryContentPath))
                {
                    File.Delete(temporaryContentPath);
                }
            }

            await WriteMetadataAsync(metadataPath, metadata, cancellationToken).ConfigureAwait(false);
            await EnforceSizeLimitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearHostAsync(string hostId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (string metadataPath in EnumerateMetadataFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                CacheMetadata? metadata = await ReadMetadataAsync(metadataPath, cancellationToken).ConfigureAwait(false);
                if (metadata?.HostId == hostId)
                {
                    DeleteEntryFiles(metadataPath, metadata);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task EnforceSizeLimitAsync(CancellationToken cancellationToken)
    {
        List<(string MetadataPath, CacheMetadata Metadata)> entries = [];
        foreach (string metadataPath in EnumerateMetadataFiles())
        {
            CacheMetadata? metadata = await ReadMetadataAsync(metadataPath, cancellationToken).ConfigureAwait(false);
            if (metadata is null || !File.Exists(GetContentPath(metadata)))
            {
                DeleteEntryFiles(metadataPath, metadata);
                continue;
            }

            entries.Add((metadataPath, metadata));
        }

        long totalBytes = entries.Sum(entry => entry.Metadata.ContentLength);
        foreach ((string metadataPath, CacheMetadata metadata) in entries
            .OrderBy(entry => entry.Metadata.LastAccessUtc)
            .ThenBy(entry => entry.Metadata.StoredUtc))
        {
            if (totalBytes <= _sizeLimitBytes)
            {
                break;
            }

            DeleteEntryFiles(metadataPath, metadata);
            totalBytes -= metadata.ContentLength;
        }
    }

    private IEnumerable<string> EnumerateMetadataFiles() =>
        Directory.Exists(_cacheRoot)
            ? Directory.EnumerateFiles(_cacheRoot, "*.json", SearchOption.TopDirectoryOnly)
            : [];

    private string GetMetadataPath(string hostId, string resourceKey) =>
        Path.Combine(_cacheRoot, $"{CreateCacheKey(hostId, resourceKey)}.json");

    private string GetContentPath(CacheMetadata metadata) =>
        Path.Combine(_cacheRoot, metadata.ContentFile);

    private static async Task<CacheMetadata?> ReadMetadataAsync(
        string metadataPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using FileStream stream = File.OpenRead(metadataPath);
            return await JsonSerializer
                .DeserializeAsync<CacheMetadata>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task WriteMetadataAsync(
        string metadataPath,
        CacheMetadata metadata,
        CancellationToken cancellationToken)
    {
        string tempPath = $"{metadataPath}.{Guid.NewGuid():N}.tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, metadataPath, overwrite: true);
    }

    private void DeleteEntryFiles(string metadataPath, CacheMetadata? metadata)
    {
        if (metadata is not null)
        {
            string contentPath = GetContentPath(metadata);
            if (File.Exists(contentPath))
            {
                File.Delete(contentPath);
            }
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }
    }

    private static string CreateCacheKey(string hostId, string resourceKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{hostId}\n{resourceKey}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void ValidateKey(string hostId, string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
    }

    private sealed class CacheMetadata
    {
        public string HostId { get; set; } = string.Empty;

        public string ResourceKey { get; set; } = string.Empty;

        public string? ETag { get; set; }

        public DateTimeOffset StoredUtc { get; set; }

        public DateTimeOffset LastAccessUtc { get; set; }

        public long ContentLength { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/octet-stream";

        public string ContentFile { get; set; } = string.Empty;
    }
}
