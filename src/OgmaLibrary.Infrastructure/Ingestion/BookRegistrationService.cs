using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Inserts new <c>Book</c> and <c>BookFile</c> rows, enqueues metadata and thumbnail
/// jobs with idempotency keys, and updates file paths for re-matched books
/// (FR-LIB-003, NFR-OGMA-009).
/// </summary>
public sealed class BookRegistrationService : IBookRegistrationService
{
    private static readonly char[] CrockfordAlphabet =
        "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="BookRegistrationService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal BookRegistrationService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BookRegistrationService"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="serviceProvider">The application service provider, used only to make DI constructor selection unambiguous.</param>
    [ActivatorUtilitiesConstructor]
    public BookRegistrationService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<string> RegisterAsync(
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        string bookId = GenerateBookId();

        context.Books.Add(new BookRow
        {
            BookId = bookId,
            Sha256Hash = contentHash,
            SizeBytes = discovered.SizeBytes,
            MtimeTicks = discovered.MtimeTicks,
            Status = 0, // Active
        });

        context.BookFiles.Add(new BookFileRow
        {
            BookId = bookId,
            RelativePath = discovered.RelativePath,
            FileStatus = 0, // Present
            LastSeenUtc = DateTimeOffset.UtcNow,
        });

        // Enqueue metadata extraction job (idempotent).
        TryAddJob(context, bookId, "MetadataExtraction",
            ComputeIdempotencyKey(bookId, "MetadataExtraction"), discovered.AbsolutePath);

        // Enqueue thumbnail generation job (idempotent).
        TryAddJob(context, bookId, "ThumbnailGeneration",
            ComputeIdempotencyKey(bookId, "ThumbnailGeneration"), discovered.AbsolutePath);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bookId;
    }

    /// <inheritdoc />
    public async Task UpdateFilePathAsync(
        string bookId,
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookFileRow? fileRow = await context.BookFiles
            .FirstOrDefaultAsync(f => f.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (fileRow is not null)
        {
            fileRow.RelativePath = discovered.RelativePath;
            fileRow.FileStatus = 0; // Present
            fileRow.LastSeenUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            context.BookFiles.Add(new BookFileRow
            {
                BookId = bookId,
                RelativePath = discovered.RelativePath,
                FileStatus = 0,
                LastSeenUtc = DateTimeOffset.UtcNow,
            });
        }

        // Re-activate the book if it was flagged as Unavailable.
        BookRow? book = await context.Books
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (book is not null)
        {
            if (book.Status == 1) // Unavailable
            {
                book.Status = 0; // Active
            }

            // Update identity attributes.
            book.Sha256Hash = contentHash;
            book.SizeBytes = discovered.SizeBytes;
            book.MtimeTicks = discovered.MtimeTicks;
        }

        TryAddJob(context, bookId, "MetadataExtraction",
            ComputeIdempotencyKey(bookId, "MetadataExtraction"), discovered.AbsolutePath);

        TryAddJob(context, bookId, "ThumbnailGeneration",
            ComputeIdempotencyKey(bookId, "ThumbnailGeneration"), discovered.AbsolutePath);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryAddJob(
        CatalogueDbContext context,
        string bookId,
        string jobType,
        string idempotencyKey,
        string filePath)
    {
        // Only add if no job with this idempotency key exists.
        bool exists = context.Jobs.Any(j => j.IdempotencyKey == idempotencyKey);
        if (!exists)
        {
            context.Jobs.Add(new JobRow
            {
                JobType = jobType,
                IdempotencyKey = idempotencyKey,
                Status = 0, // Pending
                BookId = bookId,
                Payload = filePath,
            });
        }
    }

    private static string ComputeIdempotencyKey(string bookId, string jobType)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{bookId}|{jobType}");
        byte[] hash = SHA256.HashData(data);
        // 32-hex-char idempotency key (128-bit) is sufficient for uniqueness.
        return Convert.ToHexStringLower(hash)[..32];
    }

    /// <summary>
    /// Generates a 26-character Crockford base-32 ULID-style identifier.
    /// Format: 10 chars timestamp + 16 chars random = 26 chars.
    /// </summary>
    private static string GenerateBookId()
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<char> buf = stackalloc char[26];

        // Encode timestamp in the first 10 Crockford chars (5 bits each).
        for (int i = 9; i >= 0; i--)
        {
            buf[i] = CrockfordAlphabet[(int)(ts & 0x1F)];
            ts >>= 5;
        }

        // Fill remaining 16 chars with random bits.
        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);
        int bitBuf = 0;
        int bitCount = 0;
        int ri = 0;
        for (int i = 10; i < 26; i++)
        {
            if (bitCount < 5)
            {
                bitBuf = (bitBuf << 8) | (ri < random.Length ? random[ri++] : 0);
                bitCount += 8;
            }

            buf[i] = CrockfordAlphabet[(bitBuf >> (bitCount - 5)) & 0x1F];
            bitCount -= 5;
        }

        return new string(buf);
    }

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;

        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
