using System.Buffers;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>Root-bounded PDF validator with a conservative 512 MiB input ceiling.</summary>
public sealed class PdfInputBroker : IPdfInputBroker
{
    /// <summary>Default maximum input accepted by the broker.</summary>
    public const long DefaultMaximumBytes = 512L * 1024 * 1024;

    private readonly long _maximumBytes;

    /// <summary>Initializes a broker with the default input ceiling.</summary>
    public PdfInputBroker()
        : this(DefaultMaximumBytes)
    {
    }

    /// <summary>Initializes a broker with an explicit positive input ceiling.</summary>
    public PdfInputBroker(long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _maximumBytes = maximumBytes;
    }

    /// <inheritdoc />
    public async Task<PdfInputValidationResult> ValidateAsync(
        string filePath,
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string canonicalPath;
        try
        {
            string canonicalRoot = PathGuard.CanonicalizeRoot(rootPath);
            canonicalPath = PathGuard.EnsureWithinRoot(filePath, canonicalRoot);
        }
        catch (PathTraversalException)
        {
            return Invalid(PdfInputValidationStatus.OutsideRoot);
        }
        catch (ArgumentException)
        {
            return Invalid(PdfInputValidationStatus.Unreadable);
        }

        if (!File.Exists(canonicalPath))
        {
            return Invalid(PdfInputValidationStatus.NotFound, canonicalPath);
        }

        if (!string.Equals(Path.GetExtension(canonicalPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid(PdfInputValidationStatus.InvalidExtension, canonicalPath);
        }

        FileInfo info = new(canonicalPath);
        if (info.Length > _maximumBytes)
        {
            return Invalid(PdfInputValidationStatus.TooLarge, canonicalPath, info.Length);
        }

        byte[] header = ArrayPool<byte>.Shared.Rent(5);
        try
        {
            using FileStream stream = new(
                canonicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 5,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            int read = await stream.ReadAsync(header.AsMemory(0, 5), cancellationToken)
                .ConfigureAwait(false);
            if (read != 5 || !header.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            {
                return Invalid(PdfInputValidationStatus.InvalidMagic, canonicalPath, info.Length);
            }

            return new PdfInputValidationResult(
                PdfInputValidationStatus.Valid,
                info.Length,
                canonicalPath);
        }
        catch (UnauthorizedAccessException)
        {
            return Invalid(PdfInputValidationStatus.Unreadable, canonicalPath, info.Length);
        }
        catch (IOException)
        {
            return Invalid(PdfInputValidationStatus.Unreadable, canonicalPath, info.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    private static PdfInputValidationResult Invalid(
        PdfInputValidationStatus status,
        string? canonicalPath = null,
        long sizeBytes = 0) => new(status, sizeBytes, canonicalPath);
}
