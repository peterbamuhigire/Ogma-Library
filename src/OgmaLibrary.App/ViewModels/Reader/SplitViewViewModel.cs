using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Two-pane split-view reader workspace; each pane owns a session (READ-012, D-96).</summary>
public sealed class SplitViewViewModel : INotifyPropertyChanged
{
    /// <summary>The most panes split view will ever show (D-96).</summary>
    public const int MaxPanes = 2;

    private readonly ILocalizationService _localization;
    private readonly Func<SplitPaneViewModel>? _paneFactory;
    private readonly List<SplitPaneViewModel> _panes = [];
    private bool _syncScroll;
    private bool _syncing;

    /// <summary>Initializes a split workspace that cannot open panes (design-time and tests).</summary>
    /// <param name="localization">The localization service.</param>
    public SplitViewViewModel(ILocalizationService localization)
        : this(localization, null)
    {
    }

    /// <summary>Initializes a split workspace whose panes are created on demand.</summary>
    /// <param name="localization">The localization service.</param>
    /// <param name="paneFactory">Creates one pane with its own session scope.</param>
    public SplitViewViewModel(ILocalizationService localization, Func<SplitPaneViewModel>? paneFactory)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        _paneFactory = paneFactory;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Localized title.</summary>
    public string Title => _localization["SplitView.Title"];

    /// <summary>Localized left-pane label.</summary>
    public string LeftPaneText => _localization["SplitView.LeftPane"];

    /// <summary>Localized right-pane label.</summary>
    public string RightPaneText => _localization["SplitView.RightPane"];

    /// <summary>Localized empty-pane prompt.</summary>
    public string PlaceholderText => _localization["SplitView.Placeholder"];

    /// <summary>Accessibility label for the split-reader workspace.</summary>
    public string AccessibleLabel => _localization["SplitView.AccessibleLabel"];

    /// <summary>Localized synchronised-scrolling label.</summary>
    public string SyncScrollLabel => _localization["SplitView.SyncScroll"];

    /// <summary>The live panes, at most <see cref="MaxPanes"/>.</summary>
    public IReadOnlyList<SplitPaneViewModel> Panes => _panes;

    /// <summary>The left pane, or null when split view is closed.</summary>
    public SplitPaneViewModel? LeftPane => _panes.Count > 0 ? _panes[0] : null;

    /// <summary>The right pane, or null when split view is closed.</summary>
    public SplitPaneViewModel? RightPane => _panes.Count > 1 ? _panes[1] : null;

    /// <summary>Whether a page turn in one pane follows in the other. Off by default.</summary>
    public bool SyncScroll
    {
        get => _syncScroll;
        set
        {
            if (_syncScroll != value)
            {
                _syncScroll = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Creates the panes if they do not exist, optionally opening a book on the left.</summary>
    /// <param name="leftBookId">The book to show in the left pane, or null to leave it empty.</param>
    /// <param name="cancellationToken">A token to cancel the open.</param>
    /// <returns>A task that completes when the panes exist and the optional book is open.</returns>
    public async Task ActivateAsync(string? leftBookId, CancellationToken cancellationToken = default)
    {
        if (_paneFactory is null)
        {
            return;
        }

        while (_panes.Count < MaxPanes)
        {
            SplitPaneViewModel pane = _paneFactory();
            pane.Reader.PropertyChanged += OnPaneReaderChanged;
            _panes.Add(pane);
        }

        RaisePanes();
        if (!string.IsNullOrWhiteSpace(leftBookId) && LeftPane is { } left && !left.Reader.IsOpen)
        {
            await left.OpenAsync(leftBookId, null, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Closes both panes and disposes their sessions.</summary>
    /// <returns>A task that completes when every pane session is released.</returns>
    public async Task CloseAsync()
    {
        SplitPaneViewModel[] closing = [.. _panes];
        _panes.Clear();
        RaisePanes();
        foreach (SplitPaneViewModel pane in closing)
        {
            pane.Reader.PropertyChanged -= OnPaneReaderChanged;
            await pane.DisposeAsync().ConfigureAwait(true);
        }
    }

    private void OnPaneReaderChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_syncScroll || _syncing || e.PropertyName != nameof(ReaderViewModel.CurrentPageIndex) ||
            sender is not ReaderViewModel source)
        {
            return;
        }

        SplitPaneViewModel? other = _panes.Find(p => !ReferenceEquals(p.Reader, source));
        if (other is null || !other.Reader.IsOpen || other.Reader.CurrentPageIndex == source.CurrentPageIndex)
        {
            return;
        }

        _syncing = true;
        UiActions.Run(
            async () =>
            {
                try
                {
                    await other.Reader.GoToPageAsync(source.CurrentPageIndex).ConfigureAwait(true);
                }
                finally
                {
                    _syncing = false;
                }
            },
            "reader.split.sync");
    }

    private void RaisePanes()
    {
        OnPropertyChanged(nameof(Panes));
        OnPropertyChanged(nameof(LeftPane));
        OnPropertyChanged(nameof(RightPane));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(LeftPaneText));
        OnPropertyChanged(nameof(RightPaneText));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(AccessibleLabel));
        OnPropertyChanged(nameof(SyncScrollLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiThreadGuard.Verify(this, propertyName, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
