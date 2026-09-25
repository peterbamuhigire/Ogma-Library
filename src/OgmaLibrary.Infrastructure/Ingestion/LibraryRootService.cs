using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>Persists root identities and coordinates bounded platform probes.</summary>
public sealed class LibraryRootService : ILibraryRootService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly ILibraryRootPlatformAdapter _platform;

    /// <summary>Test and migration constructor using an existing context.</summary>
    internal LibraryRootService(CatalogueDbContext context, ILibraryRootPlatformAdapter platform)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(platform);
        _context = context;
        _platform = platform;
    }

    /// <summary>DI constructor using independent contexts for concurrent callers.</summary>
    [ActivatorUtilitiesConstructor]
    public LibraryRootService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILibraryRootPlatformAdapter platform,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _contextFactory = contextFactory;
        _platform = platform;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LibraryRootDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        List<LibraryRootRow> rows = await context.LibraryRoots
            .AsNoTracking()
            .Where(row => row.RemovedUtc == null)
            .OrderBy(row => row.DisplayName)
            .ThenBy(row => row.LibraryRootId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<LibraryRootDescriptor> AddAsync(
        string path,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        string canonical = _platform.CanonicalizeRoot(path);
        LibraryRootProbeResult probe = _platform.Probe(canonical);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        LibraryRootRow? existing = await context.LibraryRoots
            .FirstOrDefaultAsync(row => row.CanonicalLocator == canonical, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.RemovedUtc is null)
            {
                throw new InvalidOperationException("The selected library root is already configured.");
            }

            // Re-adding a removed folder restores its identity, so its books return
            // with their reading progress and annotations (Sept-23 Phase 05).
            existing.RemovedUtc = null;
            existing.IsEnabled = true;
            existing.VolumeIdentity = probe.VolumeIdentity;
            existing.RootStatus = (int)probe.Status;
            existing.PermissionStatus = (int)probe.PermissionStatus;
            existing.LastHealthCheckUtc = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                existing.DisplayName = displayName.Trim();
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Map(existing);
        }

        // Sept-23 Phase 05: roots never nest, so a file belongs to exactly one root.
        List<string> activeLocators = await context.LibraryRoots
            .AsNoTracking()
            .Where(candidate => candidate.RemovedUtc == null && candidate.CanonicalLocator != null)
            .Select(candidate => candidate.CanonicalLocator!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeLocators.Any(locator => Overlaps(locator, canonical)))
        {
            throw new InvalidOperationException("The selected folder overlaps a configured library root.");
        }

        var row = new LibraryRootRow
        {
            LibraryRootId = CanonicalIdGenerator.NewId(),
            DisplayName = NormalizeDisplayName(displayName, canonical),
            CanonicalLocator = canonical,
            VolumeIdentity = probe.VolumeIdentity,
            RootStatus = (int)probe.Status,
            PermissionStatus = (int)probe.PermissionStatus,
            IsEnabled = true,
            AllowSymlinkTraversal = false,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastHealthCheckUtc = DateTimeOffset.UtcNow,
        };
        context.LibraryRoots.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task<LibraryRootDescriptor> EnsureForLegacyPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        string canonical = _platform.CanonicalizeRoot(path);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow? existing = await context.LibraryRoots
            .FirstOrDefaultAsync(row => row.CanonicalLocator == canonical, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Map(existing);
        }

        LibraryRootProbeResult probe = _platform.Probe(canonical);
        var row = new LibraryRootRow
        {
            LibraryRootId = CanonicalIdGenerator.NewId(),
            DisplayName = NormalizeDisplayName(null, canonical),
            CanonicalLocator = canonical,
            VolumeIdentity = probe.VolumeIdentity,
            RootStatus = (int)probe.Status,
            PermissionStatus = (int)probe.PermissionStatus,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastHealthCheckUtc = DateTimeOffset.UtcNow,
        };
        context.LibraryRoots.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task<LibraryRootDescriptor> RelinkAsync(
        LibraryRootId rootId,
        string path,
        CancellationToken cancellationToken = default)
    {
        string canonical = _platform.CanonicalizeRoot(path);
        LibraryRootProbeResult probe = _platform.Probe(canonical);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow row = await FindAsync(context, rootId, cancellationToken).ConfigureAwait(false);

        if (await context.LibraryRoots.AnyAsync(
                candidate => candidate.LibraryRootId != rootId.Value &&
                             candidate.CanonicalLocator == canonical,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The selected library root is already configured.");
        }

        row.CanonicalLocator = canonical;
        row.VolumeIdentity = probe.VolumeIdentity;
        row.RootStatus = (int)probe.Status;
        row.PermissionStatus = (int)probe.PermissionStatus;
        row.IsEnabled = true;
        row.LastHealthCheckUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task<LibraryRootDescriptor> SetEnabledAsync(
        LibraryRootId rootId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow row = await FindAsync(context, rootId, cancellationToken).ConfigureAwait(false);
        row.IsEnabled = isEnabled;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow row = await FindAsync(context, rootId, cancellationToken).ConfigureAwait(false);
        if (row.RemovedUtc is not null)
        {
            return;
        }

        // Soft removal: occurrences, books, progress and annotations are kept (R1 reversibility).
        row.RemovedUtc = DateTimeOffset.UtcNow;
        row.IsEnabled = false;
        context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "LibraryRootRemoved",
            EntityId = row.LibraryRootId,
            EntityType = "LibraryRoot",
            Timestamp = DateTimeOffset.UtcNow,
            IsLocalOnly = true,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LibraryRootDescriptor> RefreshHealthAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow row = await FindAsync(context, rootId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(row.CanonicalLocator))
        {
            row.RootStatus = (int)LibraryRootStatus.NeedsRelink;
            row.PermissionStatus = (int)LibraryRootPermissionStatus.Unknown;
        }
        else
        {
            LibraryRootProbeResult probe = _platform.Probe(row.CanonicalLocator);
            row.RootStatus = (int)probe.Status;
            row.PermissionStatus = (int)probe.PermissionStatus;
            row.VolumeIdentity = probe.VolumeIdentity;
        }

        row.LastHealthCheckUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(row);
    }

    /// <inheritdoc />
    public async Task RecordSuccessfulScanAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        LibraryRootRow row = await FindAsync(context, rootId, cancellationToken).ConfigureAwait(false);
        row.LastSuccessfulScanUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
    }

    private static async Task<LibraryRootRow> FindAsync(
        CatalogueDbContext context,
        LibraryRootId rootId,
        CancellationToken cancellationToken)
    {
        LibraryRootRow? row = await context.LibraryRoots
            .FirstOrDefaultAsync(candidate => candidate.LibraryRootId == rootId.Value, cancellationToken)
            .ConfigureAwait(false);
        return row ?? throw new KeyNotFoundException($"Library root '{rootId.Value}' was not found.");
    }

    private static LibraryRootDescriptor Map(LibraryRootRow row) => new(
        new LibraryRootId(row.LibraryRootId),
        row.DisplayName,
        row.CanonicalLocator,
        row.VolumeIdentity,
        (LibraryRootStatus)row.RootStatus,
        (LibraryRootPermissionStatus)row.PermissionStatus,
        row.IsEnabled,
        row.AllowSymlinkTraversal,
        row.CreatedUtc,
        row.LastHealthCheckUtc,
        row.LastSuccessfulScanUtc);

    private static bool Overlaps(string existing, string candidate)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string left = Path.TrimEndingDirectorySeparator(existing) + Path.DirectorySeparatorChar;
        string right = Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar;
        return left.StartsWith(right, comparison) || right.StartsWith(left, comparison);
    }

    private static string NormalizeDisplayName(string? displayName, string canonical)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        string name = new DirectoryInfo(canonical).Name;
        return string.IsNullOrWhiteSpace(name) ? canonical : name;
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
