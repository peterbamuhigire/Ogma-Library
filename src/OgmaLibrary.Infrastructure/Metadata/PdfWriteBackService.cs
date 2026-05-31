using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using PdfSharp.Pdf.IO;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using PdfPigParsingOptions = UglyToad.PdfPig.ParsingOptions;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// PDFsharp-backed implementation of <see cref="IMetadataWriteBackService"/> that
/// writes accepted metadata into a PDF's DocInfo under the
/// backup → diff → write (temp) → verify → rename sequence, and restores the
/// original on any failure (FR-META-005, ADR-0008, NFR-PROD-010, R1).
/// </summary>
public sealed class PdfWriteBackService : IMetadataWriteBackService
{
    private readonly CatalogueDbContext _context;
    private readonly ISidecarService _sidecarService;
    private readonly string _libraryRoot;

    /// <summary>
    /// Initializes a new instance of <see cref="PdfWriteBackService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="sidecarService">The sidecar path resolver.</param>
    /// <param name="libraryRoot">The validated library root directory.</param>
    public PdfWriteBackService(
        CatalogueDbContext context,
        ISidecarService sidecarService,
        string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sidecarService);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        _context = context;
        _sidecarService = sidecarService;
        _libraryRoot = Path.GetFullPath(libraryRoot);
    }

    /// <inheritdoc />
    public async Task<BackupToken> PrepareBackupAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);
        ValidatePathUnderLibraryRoot(absoluteFilePath);

        string sha256 = await ComputeSha256Async(absoluteFilePath, cancellationToken)
            .ConfigureAwait(false);

        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string sha8 = sha256[..8];
        string backupFileName = $"{timestamp}_pre_writeback_{sha8}.pdf";

        // Resolve sidecar path using the sha256 of the file content.
        string backupDir = Path.Combine(_libraryRoot, ".ogma", "backups");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir, backupFileName);

        await Task.Run(() => File.Copy(absoluteFilePath, backupPath, overwrite: true), cancellationToken)
            .ConfigureAwait(false);

        return new BackupToken(
            BackupAbsolutePath: backupPath,
            OriginalAbsolutePath: absoluteFilePath,
            OriginalSha256: sha256);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FieldDiff>> BuildDiffAsync(
        string absoluteFilePath,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);
        ArgumentNullException.ThrowIfNull(acceptedProposals);

        return Task.Run<IReadOnlyList<FieldDiff>>(() => BuildDiffCore(absoluteFilePath, acceptedProposals), cancellationToken);
    }

    private static List<FieldDiff> BuildDiffCore(
        string filePath,
        IReadOnlyList<AcceptedFieldProposal> proposals)
    {
        var currentDocInfo = ReadDocInfo(filePath);
        var diffs = new List<FieldDiff>();

        foreach (AcceptedFieldProposal proposal in proposals)
        {
            currentDocInfo.TryGetValue(proposal.FieldName, out string? currentValue);
            string? newValue = proposal.AcceptedValue;

            if (!string.Equals(currentValue, newValue, StringComparison.Ordinal))
            {
                diffs.Add(new FieldDiff(proposal.FieldName, currentValue, newValue));
            }
        }

        return diffs;
    }

    private static Dictionary<string, string?> ReadDocInfo(string filePath)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = PdfPigDocument.Open(filePath, new PdfPigParsingOptions { UseLenientParsing = true });
            var info = doc.Information;
            map["Title"] = info.Title;
            map["Author"] = info.Author;
            map["Subject"] = info.Subject;
            map["Creator"] = info.Creator;
        }
        catch (Exception)
        {
            // If the PDF can't be read, return empty.
        }

        return map;
    }

    /// <inheritdoc />
    public async Task<bool> WriteAsync(
        string bookId,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        BackupToken backupToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(acceptedProposals);
        ArgumentNullException.ThrowIfNull(backupToken);
        ValidatePathUnderLibraryRoot(backupToken.OriginalAbsolutePath);

        string originalPath = backupToken.OriginalAbsolutePath;
        string tempPath = originalPath + ".ogma_tmp";

        try
        {
            // Write to temp file using PDFsharp.
            await Task.Run(() => WriteToPdfSharp(originalPath, tempPath, acceptedProposals), cancellationToken)
                .ConfigureAwait(false);

            // Verify: PdfPig must be able to open the temp file.
            await Task.Run(() => VerifyPdf(tempPath), cancellationToken).ConfigureAwait(false);

            // Atomic rename: on Windows we must delete the target first.
            await Task.Run(() =>
            {
                if (File.Exists(originalPath))
                {
                    File.Delete(originalPath);
                }

                File.Move(tempPath, originalPath);
            }, cancellationToken).ConfigureAwait(false);

            // Update Books row: new sha256 and mtime.
            string newSha256 = await ComputeSha256Async(originalPath, cancellationToken)
                .ConfigureAwait(false);

            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);

            if (book is not null)
            {
                book.Sha256Hash = newSha256;
                book.MtimeTicks = new System.IO.FileInfo(originalPath).LastWriteTimeUtc.Ticks;
            }

            _context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "WriteBackSucceeded",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    fields = acceptedProposals.Select(p => p.FieldName),
                    backup = backupToken.BackupAbsolutePath,
                }),
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            await CleanupAfterFailureAsync(tempPath, backupToken, bookId, "Cancelled")
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await CleanupAfterFailureAsync(tempPath, backupToken, bookId, ex.Message)
                .ConfigureAwait(false);
            return false;
        }
    }

    private static void WriteToPdfSharp(
        string sourcePath,
        string destPath,
        IReadOnlyList<AcceptedFieldProposal> proposals)
    {
        using var document = PdfSharp.Pdf.IO.PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
        var info = document.Info;

        foreach (AcceptedFieldProposal proposal in proposals)
        {
            switch (proposal.FieldName)
            {
                case "Title":
                    info.Title = proposal.AcceptedValue ?? string.Empty;
                    break;
                case "Author":
                    info.Author = proposal.AcceptedValue ?? string.Empty;
                    break;
                case "Subject":
                    info.Subject = proposal.AcceptedValue ?? string.Empty;
                    break;
                case "Publisher":
                    // PDFsharp stores publisher in Creator field as a convention.
                    info.Creator = proposal.AcceptedValue ?? string.Empty;
                    break;
                case "Description":
                    info.Subject = proposal.AcceptedValue ?? string.Empty;
                    break;
                default:
                    // Custom keywords via Keywords property.
                    if (proposal.FieldName == "Keywords")
                    {
                        info.Keywords = proposal.AcceptedValue ?? string.Empty;
                    }

                    break;
            }
        }

        document.Save(destPath);
    }

    private static void VerifyPdf(string filePath)
    {
        // Use PdfPig to verify the file is a valid PDF.
        using var doc = PdfPigDocument.Open(filePath, new PdfPigParsingOptions { UseLenientParsing = true });
        // If we can read at least 0 pages, the file is valid.
        _ = doc.NumberOfPages;
    }

    private async Task CleanupAfterFailureAsync(
        string tempPath,
        BackupToken backupToken,
        string bookId,
        string errorMessage)
    {
        // Delete temp file if present.
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception)
        {
            // Best effort.
        }

        // Restore original from backup.
        try
        {
            if (File.Exists(backupToken.BackupAbsolutePath))
            {
                File.Copy(backupToken.BackupAbsolutePath, backupToken.OriginalAbsolutePath, overwrite: true);
            }
        }
        catch (Exception)
        {
            // Best effort; can't restore, but we still write the audit event.
        }

        try
        {
            _context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "WriteBackFailed",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    error = errorMessage,
                    backup = backupToken.BackupAbsolutePath,
                }),
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });

            await _context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best effort on audit save.
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        byte[] hashBytes = SHA256.HashData(fileBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private void ValidatePathUnderLibraryRoot(string absolutePath)
    {
        string fullPath = Path.GetFullPath(absolutePath);
        if (!fullPath.StartsWith(_libraryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Write-back path '{absolutePath}' is outside the library root '{_libraryRoot}'.");
        }
    }
}
