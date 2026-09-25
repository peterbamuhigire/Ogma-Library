using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Owns library freshness (Sept-23 Phase 05, T05.8, K22): single-flight rescans,
/// the deferred startup scan, and one debounced <see cref="FileSystemWatcher"/> per
/// enabled root. Watcher bursts are coalesced per root; an overflow or watcher error
/// falls back to an incremental scan of that root. Every scan ends in a terminal
/// <see cref="ScanOutcome"/> that is published through <see cref="ScanCompleted"/>.
/// </summary>
public sealed class LibraryMonitorService : ILibraryMonitor, IDisposable
{
    /// <summary>Quiet period after the last file event before a root is rescanned.</summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    private readonly IIngestionOrchestrator _orchestrator;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly ILibrarySettingsService _settings;
    private readonly LegacyAssetMigrationService? _assetMigration;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly SemaphoreSlim _watcherGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<string, RootWatch> _watches = new(StringComparer.Ordinal);
    private CancellationTokenSource? _currentScan;
    private int _isScanning;
    private int _disposed;

    /// <summary>DI constructor.</summary>
    [ActivatorUtilitiesConstructor]
    public LibraryMonitorService(
        IIngestionOrchestrator orchestrator,
        IDbContextFactory<CatalogueDbContext> contextFactory,
        ILibrarySettingsService settings,
        LegacyAssetMigrationService? assetMigration = null,
        ILogger<LibraryMonitorService>? logger = null)
        : this(orchestrator, settings, assetMigration, logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>Test constructor using a direct context.</summary>
    internal LibraryMonitorService(
        IIngestionOrchestrator orchestrator,
        CatalogueDbContext context,
        ILibrarySettingsService settings,
        TimeSpan debounce)
        : this(orchestrator, settings, null, null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        Debounce = debounce;
    }

    private LibraryMonitorService(
        IIngestionOrchestrator orchestrator,
        ILibrarySettingsService settings,
        LegacyAssetMigrationService? assetMigration,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(settings);
        _orchestrator = orchestrator;
        _settings = settings;
        _assetMigration = assetMigration;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public event EventHandler<ScanSummary>? ScanCompleted;

    /// <summary>The debounce window for watcher events.</summary>
    public TimeSpan Debounce { get; init; } = DefaultDebounce;

    /// <inheritdoc />
    public bool IsScanning => Volatile.Read(ref _isScanning) != 0;

    /// <inheritdoc />
    public ScanSummary? LastSummary { get; private set; }

    /// <summary>The number of roots currently watched (diagnostics and tests).</summary>
    public int WatchedRootCount => _watches.Count;

    /// <inheritdoc />
    public async Task<ScanSummary> RescanAsync(
        IReadOnlyCollection<LibraryRootId>? roots = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ScanSummary summary;
        try
        {
            await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            summary = ScanSummary.Empty(ScanOutcome.Cancelled);
            Publish(summary);
            return summary;
        }

        using var scan = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        Volatile.Write(ref _currentScan, scan);
        Interlocked.Exchange(ref _isScanning, 1);
        try
        {
            summary = await _orchestrator.ScanRootsAsync(roots, scan.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (scan.IsCancellationRequested)
        {
            summary = ScanSummary.Empty(ScanOutcome.Cancelled);
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            // The orchestrator already set the Failed phase; keep the app running.
            InfrastructureLog.LibraryScanFailed(_logger, ex);
            summary = ScanSummary.Empty(ScanOutcome.Failed);
        }
        finally
        {
            Interlocked.Exchange(ref _isScanning, 0);
            Volatile.Write(ref _currentScan, null);
            _scanGate.Release();
        }

        Publish(summary);
        return summary;
    }

    /// <inheritdoc />
    public void CancelScan()
    {
        try
        {
            Volatile.Read(ref _currentScan)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Intentionally ignored: the scan finished between the read and the cancel.
        }
    }

    /// <inheritdoc />
    public Task StartAsync(TimeSpan startupDelay, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        CancellationToken token = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _lifetime.Token)
            .Token;

        // Deferred so the first window paints before any disk work (T05.8).
        _ = Task.Run(() => RunStartupAsync(startupDelay, token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Runs the startup sequence inline (tests and diagnostics).</summary>
    /// <param name="startupDelay">Delay before any disk work.</param>
    /// <param name="cancellationToken">A token to cancel the sequence.</param>
    public async Task RunStartupAsync(TimeSpan startupDelay, CancellationToken cancellationToken)
    {
        try
        {
            if (startupDelay > TimeSpan.Zero)
            {
                await Task.Delay(startupDelay, cancellationToken).ConfigureAwait(false);
            }

            if (_assetMigration is not null)
            {
                string? legacyRoot = await _settings.GetLibraryRootAsync(cancellationToken).ConfigureAwait(false);
                await _assetMigration.MigrateAsync([legacyRoot], cancellationToken).ConfigureAwait(false);
            }

            await RefreshWatchersAsync(cancellationToken).ConfigureAwait(false);
            await RescanAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Intentionally ignored: the application is shutting down.
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            InfrastructureLog.DegradedStep(_logger, ex, nameof(LibraryMonitorService), "library.startup");
        }
    }

    /// <inheritdoc />
    public async Task RefreshWatchersAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        await _watcherGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<LibraryRootLocation> active;
            using (CatalogueContextLease lease = await CatalogueContextLease
                       .CreateAsync(_contextFactory, _context, cancellationToken)
                       .ConfigureAwait(false))
            {
                active = await LibraryRootPaths.GetActiveRootsAsync(lease.Context, cancellationToken)
                    .ConfigureAwait(false);
            }

            var wanted = active
                .Where(root => Directory.Exists(root.CanonicalPath))
                .ToDictionary(root => root.RootId, StringComparer.Ordinal);

            foreach (string rootId in _watches.Keys.ToList())
            {
                if (!wanted.TryGetValue(rootId, out LibraryRootLocation? root) ||
                    !string.Equals(_watches[rootId].Path, root.CanonicalPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (_watches.TryRemove(rootId, out RootWatch? stale))
                    {
                        stale.Dispose();
                    }
                }
            }

            foreach (LibraryRootLocation root in wanted.Values)
            {
                if (_watches.ContainsKey(root.RootId))
                {
                    continue;
                }

                RootWatch? watch = TryCreateWatch(root);
                if (watch is not null && !_watches.TryAdd(root.RootId, watch))
                {
                    watch.Dispose();
                }
            }
        }
        finally
        {
            _watcherGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        foreach (RootWatch watch in _watches.Values)
        {
            watch.Dispose();
        }

        _watches.Clear();
        _lifetime.Dispose();
    }

    private RootWatch? TryCreateWatch(LibraryRootLocation root)
    {
        try
        {
            var watcher = new FileSystemWatcher(root.CanonicalPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            var watch = new RootWatch(root.RootId, root.CanonicalPath, watcher, this);
            watcher.EnableRaisingEvents = true;
            return watch;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // A folder that cannot be watched (network share, permissions) still gets
            // the startup scan and manual Rescan.
            InfrastructureLog.LibraryWatcherFallback(_logger, ex);
            return null;
        }
    }

    private void OnWatcherSignal(string rootId)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            !_watches.TryGetValue(rootId, out RootWatch? watch))
        {
            return;
        }

        watch.Schedule(Debounce);
    }

    private void OnWatcherError(string rootId, Exception exception)
    {
        InfrastructureLog.LibraryWatcherFallback(_logger, exception);
        OnWatcherSignal(rootId);
    }

    private async Task RunDebouncedScanAsync(string rootId)
    {
        try
        {
            await RescanAsync([new LibraryRootId(rootId)], _lifetime.Token).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Intentionally ignored: the monitor was disposed while the debounce fired.
        }
        catch (Exception ex) when (!OgmaLibrary.Application.Diagnostics.ExceptionClassification.IsFatal(ex))
        {
            InfrastructureLog.DegradedStep(_logger, ex, nameof(LibraryMonitorService), "library.watcher.rescan");
        }
    }

    private void Publish(ScanSummary summary)
    {
        LastSummary = summary;
        ScanCompleted?.Invoke(this, summary);
    }

    private static bool IsRelevant(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.Contains("/.ogma/", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("/.ogma", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // PDFs, plus extension-less names (folders renamed or deleted).
        string extension = Path.GetExtension(path);
        return extension.Length == 0 || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RootWatch : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly LibraryMonitorService _owner;
        private readonly Timer _timer;
        private readonly string _rootId;

        public RootWatch(string rootId, string path, FileSystemWatcher watcher, LibraryMonitorService owner)
        {
            _rootId = rootId;
            Path = path;
            _watcher = watcher;
            _owner = owner;
            _timer = new Timer(_ => _ = _owner.RunDebouncedScanAsync(_rootId), null, Timeout.Infinite, Timeout.Infinite);
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Changed += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
        }

        public string Path { get; }

        public void Schedule(TimeSpan debounce)
        {
            try
            {
                _timer.Change(debounce, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // Intentionally ignored: the watch was disposed while an event was in flight.
            }
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _timer.Dispose();
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (IsRelevant(e.FullPath))
            {
                _owner.OnWatcherSignal(_rootId);
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (IsRelevant(e.FullPath) || IsRelevant(e.OldFullPath))
            {
                _owner.OnWatcherSignal(_rootId);
            }
        }

        private void OnError(object sender, ErrorEventArgs e) =>
            _owner.OnWatcherError(_rootId, e.GetException());
    }
}
