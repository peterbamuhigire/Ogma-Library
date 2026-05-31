using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Resolves the stable identity of a file against the catalogue using the five-tier
/// identity chain (HLD §4, FR-LIB-003):
/// <list type="number">
/// <item><term>Tier 1</term><description>Relative path lookup (cheapest).</description></item>
/// <item><term>Tier 2</term><description>SHA-256 content hash (strong identity).</description></item>
/// <item><term>Tier 3</term><description>Size + mtime fast-path (skip rehash when unchanged).</description></item>
/// <item><term>Tier 4</term><description>PDF structural fingerprint (first 1,024 bytes of content stream).</description></item>
/// <item><term>Tier 5</term><description>ISBN / DOI (last resort; survives full re-encode).</description></item>
/// </list>
/// </summary>
public sealed class BookIdentityService : IBookIdentityService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookIdentityService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public BookIdentityService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<BookMatchResult> ResolveAsync(
        string absoluteFilePath,
        string libraryRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRootPath);

        if (!File.Exists(absoluteFilePath))
        {
            return new BookMatchResult.Unresolvable($"File not found: {absoluteFilePath}");
        }

        // ── Tier 1: Relative path ────────────────────────────────────────────────
        string relativePath = ComputeRelativePath(absoluteFilePath, libraryRootPath);
        BookFileRow? byPath = await _context.BookFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.RelativePath == relativePath && f.FileStatus == 0, cancellationToken)
            .ConfigureAwait(false);

        if (byPath is not null)
        {
            return new BookMatchResult.ExactMatch(byPath.BookId);
        }

        // ── Tier 2 + 3: SHA-256 (with size+mtime fast-path) ─────────────────────
        var fileInfo = new FileInfo(absoluteFilePath);
        string sha256Hex = await ComputeSha256Async(absoluteFilePath, cancellationToken)
            .ConfigureAwait(false);

        BookRow? byHash = await _context.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Sha256Hash == sha256Hex, cancellationToken)
            .ConfigureAwait(false);

        if (byHash is not null)
        {
            return new BookMatchResult.ExactMatch(byHash.BookId);
        }

        // ── Tier 3: check if size+mtime match a known file (fast-path hint) ──────
        long sizeTicks = fileInfo.Length;
        long mtimeTicks = fileInfo.LastWriteTimeUtc.Ticks;
        BookRow? byMtime = await _context.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.SizeBytes == sizeTicks && b.MtimeTicks == mtimeTicks,
                cancellationToken)
            .ConfigureAwait(false);

        if (byMtime is not null)
        {
            // Size+mtime matched — treat as fuzzy match with high confidence.
            return new BookMatchResult.FuzzyMatch(byMtime.BookId, 0.90f);
        }

        // ── Tier 4: PDF fingerprint ──────────────────────────────────────────────
        string? fingerprint = TryExtractPdfFingerprint(absoluteFilePath);
        if (fingerprint is not null)
        {
            BookRow? byFingerprint = await _context.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.PdfFingerprint == fingerprint, cancellationToken)
                .ConfigureAwait(false);

            if (byFingerprint is not null)
            {
                return new BookMatchResult.FuzzyMatch(byFingerprint.BookId, 0.75f);
            }
        }

        // ── Tier 5: ISBN / DOI ───────────────────────────────────────────────────
        // ISBN extraction from PDF metadata requires PdfPig (Phase 05 wires this).
        // For now, return NewBook so Phase 05 can import and assign identity.
        return new BookMatchResult.NewBook();
    }

    /// <summary>
    /// Computes the SHA-256 content hash of a file asynchronously.
    /// </summary>
    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            byte[] hash = await SHA256
                .HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);

            return Convert.ToHexStringLower(hash);
        }
    }

    /// <summary>
    /// Extracts the first 1,024 bytes of the first PDF content stream as a
    /// hex-encoded fingerprint (tier 4). Returns <see langword="null"/> for non-PDF
    /// files or on any read failure.
    /// </summary>
    private static string? TryExtractPdfFingerprint(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: false);

            // Look for the first "stream\r\n" or "stream\n" marker in the PDF.
            byte[] buffer = new byte[65536];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            Span<byte> data = buffer.AsSpan(0, bytesRead);
            int streamMarker = IndexOfStreamKeyword(data);
            if (streamMarker < 0)
            {
                return null;
            }

            // Advance past "stream" + line ending.
            int start = streamMarker + 6;
            if (start < data.Length && data[start] == '\r')
            {
                start++;
            }

            if (start < data.Length && data[start] == '\n')
            {
                start++;
            }

            int end = Math.Min(start + 1024, data.Length);
            if (end <= start)
            {
                return null;
            }

            return Convert.ToHexStringLower(data[start..end].ToArray());
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static int IndexOfStreamKeyword(Span<byte> data)
    {
        ReadOnlySpan<byte> keyword = "stream"u8;
        for (int i = 0; i <= data.Length - keyword.Length; i++)
        {
            if (data[i..].StartsWith(keyword))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Computes the relative path of a file with respect to the library root,
    /// using forward-slash separators for portability (NFR-PROD-009).
    /// </summary>
    private static string ComputeRelativePath(string absoluteFilePath, string libraryRootPath)
    {
        // Normalize to trailing separator for reliable relative computation.
        string root = libraryRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (absoluteFilePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            string relative = absoluteFilePath[root.Length..];
            // Normalize to forward-slash for portable DB storage.
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        // File is outside the library root — use the full path as relative key.
        return absoluteFilePath.Replace(Path.DirectorySeparatorChar, '/');
    }
}
