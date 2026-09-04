using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Metadata.Providers;
using OgmaLibrary.Infrastructure.Sidecar;
using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>
/// Persists a validated provider cover as a deterministic local JPEG and then
/// registers its manifest provenance. The write and manifest use the same
/// sidecar-relative path; failed registration removes only a newly-created file.
/// </summary>
public sealed class ProviderCoverAssetService
{
    private readonly ISidecarService _sidecar;
    private readonly ProviderCoverImageClient _images;
    private readonly IVisualAssetService _visualAssets;

    /// <summary>Initializes the provider cover persistence boundary.</summary>
    public ProviderCoverAssetService(
        ISidecarService sidecar,
        ProviderCoverImageClient images,
        IVisualAssetService visualAssets)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(visualAssets);
        _sidecar = sidecar;
        _images = images;
        _visualAssets = visualAssets;
    }

    /// <summary>Downloads, normalizes, atomically persists, and registers provider art.</summary>
    public async Task<VisualAssetDescriptor> PersistAsync(
        string bookId,
        string contentHash,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        string normalizedHash = NormalizeHash(contentHash);
        ProviderCoverImage image = await _images
            .DownloadAsync(sourceUrl, cancellationToken)
            .ConfigureAwait(false);

        byte[] jpeg = EncodeJpeg(image.Bytes);
        string relativePath = _sidecar.ResolveRelative(normalizedHash, SidecarClass.Covers, "_provider");
        string outputPath = _sidecar.Resolve(normalizedHash, SidecarClass.Covers, "_provider");
        bool existed = File.Exists(outputPath);
        string temporaryPath = $"{outputPath}.part-{Guid.NewGuid():N}";

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, jpeg, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, outputPath, overwrite: true);
            return await _visualAssets.RegisterResolvedAsync(
                bookId,
                "provider",
                image.Sha256,
                VisualAssetKind.Cover,
                "provider",
                relativePath,
                image.WidthPx,
                image.HeightPx,
                "jpg",
                generationVersion: 1,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            if (!existed && File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            throw;
        }
    }

    private static byte[] EncodeJpeg(byte[] bytes)
    {
        using SKImage? image = SKImage.FromEncodedData(bytes);
        if (image is null)
        {
            throw new InvalidDataException("Provider cover could not be re-encoded.");
        }

        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return encoded.ToArray();
    }

    private static string NormalizeHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        string normalized = hash.Trim().ToLowerInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            ? normalized
            : throw new ArgumentException("Content hash must be a SHA-256 hexadecimal value.", nameof(hash));
    }
}
