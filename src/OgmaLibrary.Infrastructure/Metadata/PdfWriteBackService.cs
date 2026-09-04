using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Pathing;
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
    private static readonly JsonSerializerOptions PlanJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly ISidecarService _sidecarService;
    private readonly string _libraryRoot;
    private readonly ILibrarySettingsService? _settingsService;

    /// <summary>
    /// Initializes a new instance of <see cref="PdfWriteBackService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="sidecarService">The sidecar path resolver.</param>
    /// <param name="libraryRoot">The validated library root directory.</param>
    internal PdfWriteBackService(
        CatalogueDbContext context,
        ISidecarService sidecarService,
        string libraryRoot)
        : this(context, sidecarService, libraryRoot, settingsService: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PdfWriteBackService"/> with access
    /// to the active library settings. This constructor is used by the app runtime
    /// so write-back validates against the user-selected library root.
    /// </summary>
    internal PdfWriteBackService(
        CatalogueDbContext context,
        ISidecarService sidecarService,
        string libraryRoot,
        ILibrarySettingsService? settingsService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sidecarService);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        _context = context;
        _sidecarService = sidecarService;
        _libraryRoot = Path.GetFullPath(libraryRoot);
        _settingsService = settingsService;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PdfWriteBackService"/>.
    /// </summary>
    public PdfWriteBackService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ISidecarService sidecarService,
        string libraryRoot,
        ILibrarySettingsService? settingsService)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(sidecarService);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        _contextFactory = contextFactory;
        _sidecarService = sidecarService;
        _libraryRoot = Path.GetFullPath(libraryRoot);
        _settingsService = settingsService;
    }

    /// <inheritdoc />
    public async Task<BackupToken> PrepareBackupAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);
        string backupRoot = await ValidateWriteTargetAsync(bookId, absoluteFilePath, cancellationToken)
            .ConfigureAwait(false);

        string sha256 = await ComputeSha256Async(absoluteFilePath, cancellationToken)
            .ConfigureAwait(false);

        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string sha8 = sha256[..8];
        string backupFileName = $"{timestamp}_pre_writeback_{sha8}.pdf";

        // Resolve sidecar path using the sha256 of the file content.
        string backupDir = Path.Combine(backupRoot, ".ogma", "backups");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir, backupFileName);

        await Task.Run(() => File.Copy(absoluteFilePath, backupPath, overwrite: true), cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset preparedUtc = DateTimeOffset.UtcNow;
        await SaveWriteBackPlanAsync(
                new WriteBackPlan(
                    bookId,
                    new BackupToken(backupPath, absoluteFilePath, sha256),
                    preparedUtc,
                    "prepared"),
                _libraryRoot,
                cancellationToken)
            .ConfigureAwait(false);

        using (CatalogueContextLease lease = await CatalogueContextLease
                   .CreateAsync(_contextFactory, _context, cancellationToken)
                   .ConfigureAwait(false))
        {
            lease.Context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "WriteBackPrepared",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    originalSha256 = sha256,
                    backup = backupPath,
                }),
                Timestamp = preparedUtc,
                IsLocalOnly = true,
            });
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new BackupToken(
            BackupAbsolutePath: backupPath,
            OriginalAbsolutePath: absoluteFilePath,
            OriginalSha256: sha256);
    }

    /// <inheritdoc />
    public async Task<WriteBackPlan?> GetWriteBackPlanAsync(
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        string planPath = GetPlanPath(bookId);
        if (!File.Exists(planPath))
        {
            return null;
        }

        using FileStream stream = File.OpenRead(planPath);
        WriteBackPlan? plan = await JsonSerializer.DeserializeAsync<WriteBackPlan>(
                stream,
                PlanJsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        if (plan is null ||
            !string.Equals(plan.BookId, bookId, StringComparison.Ordinal) ||
            plan.BackupToken is null ||
            plan.Status is not ("prepared" or "written" or "restored"))
        {
            throw new InvalidOperationException("The durable write-back plan is invalid or has been tampered with.");
        }

        string backupRoot = await ValidateWriteTargetAsync(
                bookId,
                plan.BackupToken.OriginalAbsolutePath,
                cancellationToken)
            .ConfigureAwait(false);
        _ = PathGuard.EnsureWithinRoot(
            plan.BackupToken.BackupAbsolutePath,
            Path.Combine(backupRoot, ".ogma", "backups"));
        return plan;
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
        await ValidateWriteTargetAsync(bookId, backupToken.OriginalAbsolutePath, cancellationToken)
            .ConfigureAwait(false);

        if (!File.Exists(backupToken.BackupAbsolutePath))
        {
            throw new InvalidOperationException(
                "The write-back backup is missing. Create a new backup before writing metadata.");
        }

        string originalPath = backupToken.OriginalAbsolutePath;
        string tempPath = originalPath + ".ogma_tmp";

        try
        {
            string currentSha256 = await ComputeSha256Async(originalPath, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(currentSha256, backupToken.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The source PDF changed after the write-back preview. Create a new backup and review the diff.");
            }

            EnsureExclusiveFileAccess(originalPath);

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

            using CatalogueContextLease lease = await CatalogueContextLease
                .CreateAsync(_contextFactory, _context, cancellationToken)
                .ConfigureAwait(false);
            CatalogueDbContext context = lease.Context;

            var book = await context.Books
                .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);

            if (book is not null)
            {
                book.Sha256Hash = newSha256;
                book.MtimeTicks = new System.IO.FileInfo(originalPath).LastWriteTimeUtc.Ticks;
                book.IndexStatus = (int)SearchBookIndexStatus.NotIndexed;
                book.EmbeddingStatus = (int)SearchEmbeddingStatus.NotEmbedded;
            }

            context.AuditEvents.Add(new AuditEventRow
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

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await UpdateWriteBackPlanStatusAsync(bookId, "written", cancellationToken)
                .ConfigureAwait(false);
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

    /// <inheritdoc />
    public async Task<bool> RestoreBackupAsync(
        string bookId,
        BackupToken backupToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(backupToken);
        string backupRoot = await ValidateWriteTargetAsync(
                bookId,
                backupToken.OriginalAbsolutePath,
                cancellationToken)
            .ConfigureAwait(false);
        string backupPath = PathGuard.EnsureWithinRoot(
            backupToken.BackupAbsolutePath,
            Path.Combine(backupRoot, ".ogma", "backups"));
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException("The write-back backup is missing. Undo cannot proceed.");
        }

        string originalPath = backupToken.OriginalAbsolutePath;
        string tempPath = originalPath + ".ogma_restore_tmp";
        try
        {
            EnsureExclusiveFileAccess(originalPath);
            await Task.Run(() => File.Copy(backupPath, tempPath, overwrite: true), cancellationToken)
                .ConfigureAwait(false);
            await Task.Run(() => VerifyPdf(tempPath), cancellationToken).ConfigureAwait(false);
            await Task.Run(() =>
            {
                if (File.Exists(originalPath))
                {
                    File.Delete(originalPath);
                }

                File.Move(tempPath, originalPath);
            }, cancellationToken).ConfigureAwait(false);

            string restoredSha256 = await ComputeSha256Async(originalPath, cancellationToken)
                .ConfigureAwait(false);
            using CatalogueContextLease lease = await CatalogueContextLease
                .CreateAsync(_contextFactory, _context, cancellationToken)
                .ConfigureAwait(false);
            CatalogueDbContext context = lease.Context;
            BookRow? book = await context.Books
                .FirstOrDefaultAsync(row => row.BookId == bookId, cancellationToken)
                .ConfigureAwait(false);
            if (book is not null)
            {
                book.Sha256Hash = restoredSha256;
                book.MtimeTicks = new FileInfo(originalPath).LastWriteTimeUtc.Ticks;
                book.IndexStatus = (int)SearchBookIndexStatus.NotIndexed;
                book.EmbeddingStatus = (int)SearchEmbeddingStatus.NotEmbedded;
            }

            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "WriteBackUndone",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    backup = backupPath,
                    restoredSha256,
                    backupRetained = true,
                }),
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await UpdateWriteBackPlanStatusAsync(bookId, "restored", cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            await RecordUndoFailureAsync(bookId, backupPath, ex.Message).ConfigureAwait(false);
            return false;
        }
    }

    private static async Task SaveWriteBackPlanAsync(
        WriteBackPlan plan,
        string backupRoot,
        CancellationToken cancellationToken)
    {
        string plansRoot = Path.Combine(backupRoot, ".ogma", "writeback-plans");
        Directory.CreateDirectory(plansRoot);
        string planPath = GetPlanPath(plan.BookId, backupRoot);
        string temporaryPath = planPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, plan, PlanJsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, planPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task UpdateWriteBackPlanStatusAsync(
        string bookId,
        string status,
        CancellationToken cancellationToken)
    {
        WriteBackPlan? plan = await GetWriteBackPlanAsync(bookId, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return;
        }

        await SaveWriteBackPlanAsync(plan with { Status = status }, _libraryRoot, cancellationToken)
            .ConfigureAwait(false);
    }

    private string GetPlanPath(string bookId) => GetPlanPath(bookId, _libraryRoot);

    private static string GetPlanPath(string bookId, string root)
    {
        string id = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(bookId)));
        return Path.Combine(root, ".ogma", "writeback-plans", id + ".json");
    }

    private async Task RecordUndoFailureAsync(string bookId, string backupPath, string errorMessage)
    {
        try
        {
            using CatalogueContextLease lease = await CatalogueContextLease
                .CreateAsync(_contextFactory, _context, CancellationToken.None)
                .ConfigureAwait(false);
            lease.Context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "WriteBackUndoFailed",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    backup = backupPath,
                    error = errorMessage.Length > 4096 ? errorMessage[..4096] : errorMessage,
                }),
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });
            await lease.Context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // An undo failure must not hide the original operation error.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Best effort cleanup; the audit remains the source of truth.
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

    private static void EnsureExclusiveFileAccess(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.SequentialScan);
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

        bool restored = false;

        // Restore original from backup.
        try
        {
            if (File.Exists(backupToken.BackupAbsolutePath))
            {
                File.Copy(backupToken.BackupAbsolutePath, backupToken.OriginalAbsolutePath, overwrite: true);
                restored = true;
            }
        }
        catch (Exception)
        {
            // Best effort; can't restore, but we still write the audit event.
        }

        try
        {
            using CatalogueContextLease lease = await CatalogueContextLease
                .CreateAsync(_contextFactory, _context, CancellationToken.None)
                .ConfigureAwait(false);
            CatalogueDbContext context = lease.Context;

            context.AuditEvents.Add(new AuditEventRow
            {
                EventType = "WriteBackFailed",
                EntityId = bookId,
                EntityType = "Book",
                AfterJson = JsonSerializer.Serialize(new
                {
                    error = errorMessage,
                    backup = backupToken.BackupAbsolutePath,
                    restored,
                }),
                Timestamp = DateTimeOffset.UtcNow,
                IsLocalOnly = true,
            });

            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
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

    private async Task<string> ValidateWriteTargetAsync(
        string bookId,
        string absolutePath,
        CancellationToken cancellationToken)
    {
        string libraryRoot = await GetActiveLibraryRootAsync(cancellationToken).ConfigureAwait(false);
        string fullPath = Path.GetFullPath(absolutePath);
        string fullRoot = Path.GetFullPath(libraryRoot);

        try
        {
            PathGuard.EnsureWithinRoot(fullPath, fullRoot);
            return fullRoot;
        }
        catch (PathTraversalException)
        {
            // A legacy direct-open file may be registered as an exact external
            // absolute path; that compatibility path is checked below by exact
            // equality and is never accepted through prefix matching.
        }

        if (await IsRegisteredAbsoluteFileAsync(bookId, fullPath, cancellationToken)
                .ConfigureAwait(false))
        {
            return Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException(
                    $"Write-back path '{absolutePath}' has no containing directory.");
        }

        throw new InvalidOperationException(
            $"Write-back path '{absolutePath}' is outside the library root '{fullRoot}' and is not the registered absolute file for book '{bookId}'.");
    }

    private async Task<string> GetActiveLibraryRootAsync(CancellationToken cancellationToken)
    {
        if (_settingsService is not null)
        {
            string? configuredRoot = await _settingsService
                .GetLibraryRootAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(configuredRoot))
            {
                return configuredRoot;
            }
        }

        return _libraryRoot;
    }

    private async Task<bool> IsRegisteredAbsoluteFileAsync(
        string bookId,
        string fullPath,
        CancellationToken cancellationToken)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        List<string> storedPaths = await context.BookFiles
            .AsNoTracking()
            .Where(f => f.BookId == bookId && f.FileStatus == 0)
            .Select(f => f.RelativePath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (string storedPath in storedPaths)
        {
            string platformPath = storedPath.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathFullyQualified(platformPath))
            {
                continue;
            }

            if (string.Equals(
                Path.GetFullPath(platformPath),
                fullPath,
                PathComparison))
            {
                return true;
            }
        }

        return false;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
