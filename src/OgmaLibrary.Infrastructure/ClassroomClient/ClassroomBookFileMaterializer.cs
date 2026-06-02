using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Writes Host file-stream resources to stable local PDF files for PDFium.</summary>
internal sealed class ClassroomBookFileMaterializer : IClassroomBookFileMaterializer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ILibraryHostClient _hostClient;
    private readonly string _filesRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ClassroomBookFileMaterializer(string dataDirectory, ILibraryHostClient hostClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _hostClient = hostClient ?? throw new ArgumentNullException(nameof(hostClient));
        _filesRoot = Path.Combine(dataDirectory, "classroom", "files");
    }

    public async Task<string> MaterializeAsync(
        ClassroomJoinRequest request,
        string sessionToken,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        LibraryHostResource resource = await _hostClient
            .GetFileStreamAsync(request, sessionToken, bookId, cancellationToken)
            .ConfigureAwait(false);

        ValidatePdfResource(resource);

        string hostKey = HostTrustService.CreateHostKey(request);
        string materializedKey = CreateStableKey(hostKey, resource.ResourceKey);
        string pdfPath = Path.Combine(_filesRoot, $"{materializedKey}.pdf");
        string metadataPath = Path.Combine(_filesRoot, $"{materializedKey}.json");
        string contentHash = Convert.ToHexString(SHA256.HashData(resource.Content)).ToLowerInvariant();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_filesRoot);
            if (await CanReuseAsync(metadataPath, pdfPath, resource, contentHash, cancellationToken)
                    .ConfigureAwait(false))
            {
                return pdfPath;
            }

            string tempPdfPath = $"{pdfPath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(tempPdfPath, resource.Content, cancellationToken).ConfigureAwait(false);
            File.Move(tempPdfPath, pdfPath, overwrite: true);

            var metadata = new MaterializedBookFileMetadata
            {
                HostId = hostKey,
                ResourceKey = resource.ResourceKey,
                BookId = bookId,
                ETag = resource.ETag,
                ContentType = resource.ContentType,
                ContentHash = contentHash,
                StoredUtc = DateTimeOffset.UtcNow,
                FileName = Path.GetFileName(pdfPath),
            };

            string tempMetadataPath = $"{metadataPath}.{Guid.NewGuid():N}.tmp";
            using (FileStream stream = File.Create(tempMetadataPath))
            {
                await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempMetadataPath, metadataPath, overwrite: true);
            return pdfPath;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<bool> CanReuseAsync(
        string metadataPath,
        string pdfPath,
        LibraryHostResource resource,
        string contentHash,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(metadataPath) || !File.Exists(pdfPath))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(metadataPath);
            MaterializedBookFileMetadata? metadata = await JsonSerializer
                .DeserializeAsync<MaterializedBookFileMetadata>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return metadata is not null &&
                metadata.ResourceKey == resource.ResourceKey &&
                metadata.ETag == resource.ETag &&
                metadata.ContentHash == contentHash;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void ValidatePdfResource(LibraryHostResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.Content.Length == 0)
        {
            throw new InvalidDataException("Host returned an empty PDF resource.");
        }

        bool contentTypeIsPdf = resource.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
        if (!contentTypeIsPdf && !LooksLikePdf(resource.Content))
        {
            throw new InvalidDataException("Host file-stream response is not a PDF resource.");
        }
    }

    private static bool LooksLikePdf(byte[] content) =>
        content.Length >= 5 &&
        content[0] == (byte)'%' &&
        content[1] == (byte)'P' &&
        content[2] == (byte)'D' &&
        content[3] == (byte)'F' &&
        content[4] == (byte)'-';

    private static string CreateStableKey(string hostKey, string resourceKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{hostKey}\n{resourceKey}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose() => _gate.Dispose();

    private sealed class MaterializedBookFileMetadata
    {
        public string HostId { get; set; } = string.Empty;

        public string ResourceKey { get; set; } = string.Empty;

        public string BookId { get; set; } = string.Empty;

        public string? ETag { get; set; }

        public string ContentType { get; set; } = "application/pdf";

        public string ContentHash { get; set; } = string.Empty;

        public DateTimeOffset StoredUtc { get; set; }

        public string FileName { get; set; } = string.Empty;
    }
}
