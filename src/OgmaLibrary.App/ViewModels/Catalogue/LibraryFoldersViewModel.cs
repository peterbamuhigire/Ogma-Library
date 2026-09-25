using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>One configured library folder as shown in the Library folders panel.</summary>
public sealed class LibraryFolderItem
{
    /// <summary>Initializes a folder row.</summary>
    public LibraryFolderItem(LibraryRootDescriptor root, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(localization);
        Root = root;
        DisplayName = root.DisplayName;
        Location = root.CanonicalLocator ?? string.Empty;
        StatusText = !root.IsEnabled
            ? localization["Library.Folders.Status.Hidden"]
            : root.Status switch
            {
                LibraryRootStatus.Unavailable => localization["Library.Folders.Status.Offline"],
                LibraryRootStatus.PermissionDenied => localization["Library.Folders.Status.PermissionDenied"],
                LibraryRootStatus.NeedsRelink => localization["Library.Folders.Status.NeedsRelink"],
                _ => localization["Library.Folders.Status.Available"],
            };
        CultureInfo culture = CultureInfo.CurrentCulture;
        AutomationName = string.Format(culture, localization["Library.Folders.ItemAutomationFormat"], DisplayName, StatusText);
        RescanAutomationName = string.Format(culture, localization["Library.Folders.RescanAutomationFormat"], DisplayName);
        RemoveAutomationName = string.Format(culture, localization["Library.Folders.RemoveAutomationFormat"], DisplayName);
        IncludeAutomationName = string.Format(culture, localization["Library.Folders.IncludeAutomationFormat"], DisplayName);
        RescanText = localization["Library.Folders.Rescan"];
        RemoveText = localization["Library.Folders.Remove"];
        IncludeText = localization["Library.Folders.Include"];
    }

    /// <summary>The underlying root.</summary>
    public LibraryRootDescriptor Root { get; }

    /// <summary>The stable root id.</summary>
    public LibraryRootId Id => Root.Id;

    /// <summary>The folder's display name.</summary>
    public string DisplayName { get; }

    /// <summary>The folder's absolute location (shown as a tooltip).</summary>
    public string Location { get; }

    /// <summary>Whether the folder's books are shown and it is scanned.</summary>
    public bool IsEnabled => Root.IsEnabled;

    /// <summary>Localized health or visibility text.</summary>
    public string StatusText { get; }

    /// <summary>Accessible name of the row.</summary>
    public string AutomationName { get; }

    /// <summary>Accessible name of the per-folder rescan button.</summary>
    public string RescanAutomationName { get; }

    /// <summary>Accessible name of the remove button.</summary>
    public string RemoveAutomationName { get; }

    /// <summary>Accessible name of the include toggle.</summary>
    public string IncludeAutomationName { get; }

    /// <summary>Localized rescan label.</summary>
    public string RescanText { get; }

    /// <summary>Localized remove label.</summary>
    public string RemoveText { get; }

    /// <summary>Localized include label.</summary>
    public string IncludeText { get; }
}

/// <summary>One file in the Needs attention list.</summary>
public sealed class NeedsAttentionEntry
{
    /// <summary>Initializes an entry.</summary>
    public NeedsAttentionEntry(NeedsAttentionItem item, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localization);
        Item = item;
        ReasonText = ReasonLabel(item.Reason, localization);
        CultureInfo culture = CultureInfo.CurrentCulture;
        AutomationName = string.Format(culture, localization["Library.Attention.ItemAutomationFormat"], item.FileName, ReasonText);
        RetryAutomationName = string.Format(culture, localization["Library.Attention.RetryAutomationFormat"], item.FileName);
        ShowAutomationName = string.Format(culture, localization["Library.Attention.ShowAutomationFormat"], item.FileName);
        IgnoreAutomationName = string.Format(culture, localization["Library.Attention.IgnoreAutomationFormat"], item.FileName);
        RetryText = localization["Library.Attention.Retry"];
        ShowText = localization["Library.Attention.ShowInFolder"];
        IgnoreText = localization["Library.Attention.Ignore"];
    }

    /// <summary>The underlying issue.</summary>
    public NeedsAttentionItem Item { get; }

    /// <summary>The file name.</summary>
    public string FileName => Item.FileName;

    /// <summary>The owning folder's name.</summary>
    public string FolderName => Item.RootName;

    /// <summary>Localized reason.</summary>
    public string ReasonText { get; }

    /// <summary>Accessible name of the row.</summary>
    public string AutomationName { get; }

    /// <summary>Accessible name of Retry.</summary>
    public string RetryAutomationName { get; }

    /// <summary>Accessible name of Show in folder.</summary>
    public string ShowAutomationName { get; }

    /// <summary>Accessible name of Ignore.</summary>
    public string IgnoreAutomationName { get; }

    /// <summary>Localized Retry label.</summary>
    public string RetryText { get; }

    /// <summary>Localized Show in folder label.</summary>
    public string ShowText { get; }

    /// <summary>Localized Ignore label.</summary>
    public string IgnoreText { get; }

    /// <summary>Returns the localized label for a validity reason.</summary>
    public static string ReasonLabel(FileValidity reason, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return reason switch
        {
            FileValidity.Empty => localization["Library.Attention.Reason.Empty"],
            FileValidity.NotAPdf => localization["Library.Attention.Reason.NotAPdf"],
            FileValidity.Locked => localization["Library.Attention.Reason.Locked"],
            _ => localization["Library.Attention.Reason.Damaged"],
        };
    }
}

/// <summary>
/// The Library folders panel (Sept-23 Phase 05, D-03): add, remove, show/hide and
/// rescan library folders, and review files that need attention. Phase 08 moves the
/// panel into Settings; the behaviour lives here so it can move without change.
/// </summary>
public sealed class LibraryFoldersViewModel : INotifyPropertyChanged
{
    private readonly ILibraryRootService _roots;
    private readonly ILibraryMonitor _monitor;
    private readonly ILibraryAttentionService _attention;
    private readonly ILocalizationService _localization;
    private readonly IUiDispatcher _ui;
    private readonly ILogger _logger;
    private int _needsAttentionCount;
    private bool _isNeedsAttentionOpen;
    private string? _statusText;
    private string? _lastSummaryText;

    /// <summary>Initializes the panel.</summary>
    public LibraryFoldersViewModel(
        ILibraryRootService roots,
        ILibraryMonitor monitor,
        ILibraryAttentionService attention,
        ILocalizationService localization,
        IUiDispatcher? uiDispatcher = null,
        ILogger<LibraryFoldersViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(attention);
        ArgumentNullException.ThrowIfNull(localization);
        _roots = roots;
        _monitor = monitor;
        _attention = attention;
        _localization = localization;
        _ui = uiDispatcher ?? InlineUiDispatcher.Instance;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _localization.CultureChanged += (_, _) => _ui.Post(RaiseLabelsChanged);
        _monitor.ScanCompleted += OnScanCompleted;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when folder changes affect which books the catalogue shows.</summary>
    public event EventHandler? CatalogueChanged;

    /// <summary>Configured folders.</summary>
    public ObservableCollection<LibraryFolderItem> Folders { get; } = [];

    /// <summary>Files that need attention.</summary>
    public ObservableCollection<NeedsAttentionEntry> NeedsAttention { get; } = [];

    /// <summary>The monitor that owns scanning.</summary>
    public ILibraryMonitor Monitor => _monitor;

    /// <summary>Panel heading.</summary>
    public string Title => _localization["Library.Folders.Title"];

    /// <summary>Add-folder label.</summary>
    public string AddFolderText => _localization["Library.Folders.Add"];

    /// <summary>Add-folder accessible name.</summary>
    public string AddFolderAutomationName => _localization["Library.Folders.AddAutomation"];

    /// <summary>Rescan-all label.</summary>
    public string RescanAllText => _localization["Library.Folders.RescanAll"];

    /// <summary>Empty-state text.</summary>
    public string EmptyText => _localization["Library.Folders.Empty"];

    /// <summary>Whether no folder is configured.</summary>
    public bool HasNoFolders => Folders.Count == 0;

    /// <summary>Needs-attention heading.</summary>
    public string NeedsAttentionTitle => _localization["Library.Attention.Title"];

    /// <summary>Needs-attention hint.</summary>
    public string NeedsAttentionHint => _localization["Library.Attention.Hint"];

    /// <summary>Needs-attention empty text.</summary>
    public string NeedsAttentionEmptyText => _localization["Library.Attention.None"];

    /// <summary>Number of files that need attention.</summary>
    public int NeedsAttentionCount
    {
        get => _needsAttentionCount;
        private set
        {
            if (_needsAttentionCount != value)
            {
                _needsAttentionCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNeedsAttention));
                OnPropertyChanged(nameof(NeedsAttentionButtonText));
            }
        }
    }

    /// <summary>Whether any file needs attention.</summary>
    public bool HasNeedsAttention => _needsAttentionCount > 0;

    /// <summary>The sidebar badge button text, e.g. "Needs attention (3)".</summary>
    public string NeedsAttentionButtonText => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Library.Attention.ButtonFormat"],
        _needsAttentionCount);

    /// <summary>Whether the needs-attention list is expanded.</summary>
    public bool IsNeedsAttentionOpen
    {
        get => _isNeedsAttentionOpen;
        set
        {
            if (_isNeedsAttentionOpen != value)
            {
                _isNeedsAttentionOpen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Transient feedback from the last folder action.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>Whether <see cref="StatusText"/> is set.</summary>
    public bool HasStatus => !string.IsNullOrWhiteSpace(_statusText);

    /// <summary>"Added 12 · Updated 0 · Needs attention 4 · Missing 0" for the last scan.</summary>
    public string? LastSummaryText
    {
        get => _lastSummaryText;
        private set
        {
            if (_lastSummaryText != value)
            {
                _lastSummaryText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastSummary));
            }
        }
    }

    /// <summary>Whether a last-scan summary is available.</summary>
    public bool HasLastSummary => !string.IsNullOrWhiteSpace(_lastSummaryText);

    /// <summary>Formats a scan summary for display.</summary>
    public string FormatSummary(ScanSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return string.Format(
            CultureInfo.CurrentCulture,
            _localization["Scan.Summary.Format"],
            summary.Added,
            summary.Updated + summary.Restored,
            summary.NeedsAttention,
            summary.Missing);
    }

    /// <summary>
    /// A folder named by <c>OGMA_LIBRARY_ROOT</c>. When set it is added to the library
    /// at startup (if missing) so the startup scan includes it (K22).
    /// </summary>
    public string? ConfiguredRoot { get; init; }

    /// <summary>
    /// Loads the panel, registers the configured root, and starts the monitor's
    /// deferred startup scan and file watchers.
    /// </summary>
    public async Task StartAsync(TimeSpan startupDelay, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(ConfiguredRoot) && Directory.Exists(ConfiguredRoot))
        {
            string configured = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ConfiguredRoot));
            IReadOnlyList<LibraryRootDescriptor> existing = await _roots.ListAsync(cancellationToken).ConfigureAwait(false);
            bool known = existing.Any(root =>
                root.CanonicalLocator is { } locator &&
                string.Equals(
                    Path.TrimEndingDirectorySeparator(locator),
                    configured,
                    StringComparison.OrdinalIgnoreCase));
            if (!known)
            {
                try
                {
                    await _roots.AddAsync(configured, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    // Already configured under another spelling of the same path.
                    AppLog.ViewModelStepIgnored(_logger, ex, nameof(LibraryFoldersViewModel), "library.configured_root");
                }
            }
        }

        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await _monitor.StartAsync(startupDelay, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reloads folders and the needs-attention list.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LibraryRootDescriptor> roots = await _roots.ListAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<NeedsAttentionItem> issues = await _attention.ListAsync(cancellationToken).ConfigureAwait(false);
        var folderItems = roots
            .Where(root => !string.IsNullOrWhiteSpace(root.CanonicalLocator))
            .Select(root => new LibraryFolderItem(root, _localization))
            .ToList();
        var issueItems = issues.Select(issue => new NeedsAttentionEntry(issue, _localization)).ToList();
        await _ui.InvokeAsync(
            () =>
            {
                Folders.Clear();
                foreach (LibraryFolderItem item in folderItems)
                {
                    Folders.Add(item);
                }

                NeedsAttention.Clear();
                foreach (NeedsAttentionEntry entry in issueItems)
                {
                    NeedsAttention.Add(entry);
                }

                NeedsAttentionCount = issueItems.Count;
                OnPropertyChanged(nameof(HasNoFolders));
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a folder to the library and scans it. Adding never relinks or hides
    /// another folder (Sept-23 Phase 05, T05.3).
    /// </summary>
    /// <returns>The added root, or null when it could not be added.</returns>
    public async Task<LibraryRootDescriptor?> AddFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LibraryRootDescriptor root;
        try
        {
            root = await _roots.AddAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await SetStatusAsync(_localization["Library.Folders.AlreadyAdded"]).ConfigureAwait(false);
            return null;
        }

        await SetStatusAsync(string.Format(
            CultureInfo.CurrentCulture,
            _localization["Library.Folders.AddedFormat"],
            root.DisplayName)).ConfigureAwait(false);
        await AfterRootsChangedAsync(cancellationToken).ConfigureAwait(false);
        _ = RunScanAsync([root.Id]);
        return root;
    }

    /// <summary>Removes a folder: its books are hidden, never deleted.</summary>
    public async Task RemoveFolderAsync(LibraryFolderItem folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        await _roots.RemoveAsync(folder.Id, cancellationToken).ConfigureAwait(false);
        await SetStatusAsync(string.Format(
            CultureInfo.CurrentCulture,
            _localization["Library.Folders.RemovedFormat"],
            folder.DisplayName)).ConfigureAwait(false);
        await AfterRootsChangedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Shows or hides a folder's books.</summary>
    public async Task SetIncludedAsync(
        LibraryFolderItem folder,
        bool isIncluded,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (folder.IsEnabled == isIncluded)
        {
            return;
        }

        await _roots.SetEnabledAsync(folder.Id, isIncluded, cancellationToken).ConfigureAwait(false);
        await AfterRootsChangedAsync(cancellationToken).ConfigureAwait(false);
        if (isIncluded)
        {
            _ = RunScanAsync([folder.Id]);
        }
    }

    /// <summary>Points a moved folder at its new location (explicit "This folder moved").</summary>
    public async Task RelinkFolderAsync(
        LibraryFolderItem folder,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        await _roots.RelinkAsync(folder.Id, newPath, cancellationToken).ConfigureAwait(false);
        await AfterRootsChangedAsync(cancellationToken).ConfigureAwait(false);
        _ = RunScanAsync([folder.Id]);
    }

    /// <summary>Rescans one folder, or every enabled folder when <paramref name="folder"/> is null.</summary>
    public Task<ScanSummary> RescanAsync(LibraryFolderItem? folder = null, CancellationToken cancellationToken = default) =>
        _monitor.RescanAsync(folder is null ? null : [folder.Id], cancellationToken);

    /// <summary>Re-checks one file; a file that is now valid is catalogued by a rescan of its folder.</summary>
    public async Task RetryAsync(NeedsAttentionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        bool valid = await _attention.RetryAsync(entry.Item.IssueId, cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (valid)
        {
            _ = RunScanAsync(entry.Item.RootId is { } rootId ? [rootId] : null);
        }
    }

    /// <summary>Hides one file from the list until it changes.</summary>
    public async Task IgnoreAsync(NeedsAttentionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _attention.IgnoreAsync(entry.Item.IssueId, cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the platform file manager with the file selected.</summary>
    public async Task ShowInFolderAsync(NeedsAttentionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string? path = await _attention.GetAbsolutePathAsync(entry.Item.IssueId, cancellationToken).ConfigureAwait(false);
        if (path is null || !File.Exists(path))
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var start = new ProcessStartInfo { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            start.FileName = "explorer.exe";
            start.ArgumentList.Add("/select," + path);
        }
        else if (OperatingSystem.IsMacOS())
        {
            start.FileName = "open";
            start.ArgumentList.Add("-R");
            start.ArgumentList.Add(path);
        }
        else
        {
            return;
        }

        using Process? process = Process.Start(start);
    }

    private async Task AfterRootsChangedAsync(CancellationToken cancellationToken)
    {
        await _monitor.RefreshWatchersAsync(cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        CatalogueChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunScanAsync(IReadOnlyCollection<LibraryRootId>? roots)
    {
        try
        {
            await _monitor.RescanAsync(roots).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(LibraryFoldersViewModel), "library.rescan");
        }
    }

    private void OnScanCompleted(object? sender, ScanSummary summary)
    {
        // A scan with no folders to scan (first run) has nothing worth summarising.
        string? text = summary.RootsScanned == 0 && summary.RootsOffline == 0 && summary.Outcome == ScanOutcome.Completed
            ? null
            : FormatSummary(summary);
        _ui.Post(() => LastSummaryText = text);
        _ = ReloadAfterScanAsync();
    }

    private async Task ReloadAfterScanAsync()
    {
        try
        {
            await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(LibraryFoldersViewModel), "library.reload");
        }
    }

    private Task SetStatusAsync(string text) => _ui.InvokeAsync(() => StatusText = text);

    private void RaiseLabelsChanged()
    {
        foreach (string name in new[]
                 {
                     nameof(Title), nameof(AddFolderText), nameof(AddFolderAutomationName), nameof(RescanAllText),
                     nameof(EmptyText), nameof(NeedsAttentionTitle), nameof(NeedsAttentionHint),
                     nameof(NeedsAttentionEmptyText), nameof(NeedsAttentionButtonText),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
