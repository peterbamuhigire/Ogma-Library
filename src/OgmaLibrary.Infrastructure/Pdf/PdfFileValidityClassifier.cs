using System.Buffers;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Discovery-time PDF validity check (Sept-23 Phase 05, T05.6, K21). It reads at
/// most the first and last few kilobytes of a file and never parses PDF objects,
/// so untrusted input stays outside a PDF engine in the scanning process:
/// <list type="bullet">
/// <item>0 bytes → <see cref="FileValidity.Empty"/>;</item>
/// <item>no <c>%PDF-</c> in the first 1,024 bytes → <see cref="FileValidity.NotAPdf"/>
/// (the same signature <see cref="PdfInputBroker"/> checks before worker operations);</item>
/// <item>no <c>%%EOF</c> in the last <see cref="TailBytes"/> bytes → <see cref="FileValidity.Damaged"/>
/// (truncated downloads);</item>
/// <item>an <c>/Encrypt</c> dictionary reference in the trailer region or the
/// linearization header → <see cref="FileValidity.Locked"/>.</item>
/// </list>
/// Structural damage beyond truncation is detected later by the isolated worker
/// when metadata extraction opens the file.
/// </summary>
public sealed class PdfFileValidityClassifier : IPdfFileValidityClassifier
{
    /// <summary>Bytes searched for the PDF signature.</summary>
    public const int HeaderBytes = 1024;

    /// <summary>Bytes searched from the end of the file for <c>%%EOF</c> and the trailer.</summary>
    public const int TailBytes = 4096;

    private const int LinearizedHeadBytes = 4096;

    private static ReadOnlySpan<byte> Signature => "%PDF-"u8;

    private static ReadOnlySpan<byte> EndOfFile => "%%EOF"u8;

    private static ReadOnlySpan<byte> EncryptKey => "/Encrypt"u8;

    /// <inheritdoc />
    public async Task<FileValidity> ClassifyAsync(
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            long length = stream.Length;
            if (length == 0)
            {
                return FileValidity.Empty;
            }

            int headLength = (int)Math.Min(Math.Max(HeaderBytes, LinearizedHeadBytes), length);
            byte[] head = ArrayPool<byte>.Shared.Rent(headLength);
            byte[] tail = ArrayPool<byte>.Shared.Rent(TailBytes);
            try
            {
                int headRead = await ReadAtAsync(stream, 0, head, headLength, cancellationToken)
                    .ConfigureAwait(false);
                ReadOnlySpan<byte> signatureWindow = head.AsSpan(0, Math.Min(headRead, HeaderBytes));
                if (signatureWindow.IndexOf(Signature) < 0)
                {
                    return FileValidity.NotAPdf;
                }

                int tailLength = (int)Math.Min(TailBytes, length);
                int tailRead = await ReadAtAsync(stream, length - tailLength, tail, tailLength, cancellationToken)
                    .ConfigureAwait(false);
                ReadOnlySpan<byte> tailWindow = tail.AsSpan(0, tailRead);
                if (tailWindow.IndexOf(EndOfFile) < 0)
                {
                    return FileValidity.Damaged;
                }

                if (tailWindow.IndexOf(EncryptKey) >= 0 ||
                    head.AsSpan(0, headRead).IndexOf(EncryptKey) >= 0)
                {
                    return FileValidity.Locked;
                }

                return FileValidity.Valid;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(head);
                ArrayPool<byte>.Shared.Return(tail);
            }
        }
    }

    private static async Task<int> ReadAtAsync(
        FileStream stream,
        long offset,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken)
    {
        stream.Position = offset;
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, count - total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
