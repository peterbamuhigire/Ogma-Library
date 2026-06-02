using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>File-backed opaque profile sync blob store for LAN Host mode.</summary>
internal sealed class FileProfileSyncBlobStore : IProfileSyncBlobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _root;

    public FileProfileSyncBlobStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _root = Path.Combine(dataDirectory, "LanHost", "profile-sync");
    }

    public async Task SaveAsync(
        string clientId,
        HostProfileSyncBlob blob,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(blob);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_root);
        string key = CreateClientKey(clientId);
        string payloadPath = Path.Combine(_root, $"{key}.blob");
        string metadataPath = Path.Combine(_root, $"{key}.json");
        string tempPayloadPath = $"{payloadPath}.{Guid.NewGuid():N}.tmp";
        string tempMetadataPath = $"{metadataPath}.{Guid.NewGuid():N}.tmp";

        await File.WriteAllBytesAsync(tempPayloadPath, blob.Content, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                tempMetadataPath,
                JsonSerializer.Serialize(new BlobMetadata(blob.ContentType, blob.UpdatedUtc), JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        File.Move(tempPayloadPath, payloadPath, overwrite: true);
        File.Move(tempMetadataPath, metadataPath, overwrite: true);
    }

    public async Task<HostProfileSyncBlob?> LoadAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        cancellationToken.ThrowIfCancellationRequested();

        string key = CreateClientKey(clientId);
        string payloadPath = Path.Combine(_root, $"{key}.blob");
        string metadataPath = Path.Combine(_root, $"{key}.json");
        if (!File.Exists(payloadPath) || !File.Exists(metadataPath))
        {
            return null;
        }

        BlobMetadata? metadata = JsonSerializer.Deserialize<BlobMetadata>(
            await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false),
            JsonOptions);
        if (metadata is null)
        {
            return null;
        }

        byte[] content = await File.ReadAllBytesAsync(payloadPath, cancellationToken)
            .ConfigureAwait(false);
        return new HostProfileSyncBlob(metadata.ContentType, content, metadata.UpdatedUtc);
    }

    internal static string CreateClientKey(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        byte[] bytes = Encoding.UTF8.GetBytes(clientId.Trim());
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private sealed record BlobMetadata(string ContentType, DateTimeOffset UpdatedUtc);
}
