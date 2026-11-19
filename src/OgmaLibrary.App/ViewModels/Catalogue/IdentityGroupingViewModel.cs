using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Copies and editions actions on the catalogue selection (Sept-23 Phase 10, task 10.9, CAT-007):
/// Merge into one work, Split and Compare, each change offering Undo in a notification. Grouping
/// only records reviewed identity; no file is ever deleted, moved or renamed.
/// </summary>
/// <remarks>
/// The grouping service has no expected-version parameter, so Undo compares the group version
/// returned by this view model's own operation with the stored one first; a group changed
/// elsewhere surfaces as a localised conflict instead of being overwritten. The check is
/// optimistic, not atomic (recorded in the Phase 10 completion record).
/// </remarks>
public sealed class IdentityGroupingViewModel : INotifyPropertyChanged
{
    private const string Actor = "local-user";

    private readonly CatalogueViewModel _catalogue;
    private readonly IIdentityGroupingService _grouping;
    private readonly IBookOccurrenceReader _occurrences;
    private readonly ILocalizationService _localization;
    private readonly IUserNotifier _notifier;
    private readonly ILogger _logger;
    private bool _isBusy;

    /// <summary>Initializes a new instance of the <see cref="IdentityGroupingViewModel"/> class.</summary>
    /// <param name="catalogue">The catalogue whose selection the actions apply to.</param>
    /// <param name="grouping">Audited, reversible identity grouping.</param>
    /// <param name="occurrences">Book-to-occurrence mapping and file facts.</param>
    /// <param name="localization">The localisation service.</param>
    /// <param name="notifier">The notification centre.</param>
    /// <param name="logger">Optional logger.</param>
    public IdentityGroupingViewModel(
        CatalogueViewModel catalogue,
        IIdentityGroupingService grouping,
        IBookOccurrenceReader occurrences,
        ILocalizationService localization,
        IUserNotifier notifier,
        ILogger<IdentityGroupingViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(grouping);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notifier);
        _catalogue = catalogue;
        _grouping = grouping;
        _occurrences = occurrences;
        _localization = localization;
        _notifier = notifier;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _catalogue.SelectedBooks.CollectionChanged += OnSelectionChanged;
        _catalogue.SelectionRestored += (_, _) => RaiseState();
        _localization.CultureChanged += (_, _) => RaiseText();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Whether a grouping operation is running.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Whether two or more selected books can be merged into one work.</summary>
    public bool CanMerge => !_isBusy && _catalogue.SelectedBooks.Count >= 2;

    /// <summary>Whether the single selected card is a reviewed work that can be split.</summary>
    public bool CanSplit => !_isBusy && _catalogue.SelectedBooks.Count == 1 && _catalogue.SelectedBooks[0].WorkGroupId is not null;

    /// <summary>Whether the selection has more than one file to compare.</summary>
    public bool CanCompare => !_isBusy && (_catalogue.SelectedBooks.Count >= 2 ||
        (_catalogue.SelectedBooks.Count == 1 &&
         (_catalogue.SelectedBooks[0].OccurrenceCount > 1 || _catalogue.SelectedBooks[0].EditionCount > 1)));

    /// <summary>Label of Merge into one work.</summary>
    public string MergeText => _localization["Catalogue.Selection.Merge"];

    /// <summary>Label of Split.</summary>
    public string SplitText => _localization["Catalogue.Selection.Split"];

    /// <summary>Label of Compare.</summary>
    public string CompareText => _localization["Catalogue.Selection.Compare"];

    /// <summary>
    /// Merges the selected books into one work. Ungrouped books form a new work; books joining one
    /// existing work are added to it; books already in two different works are refused.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task MergeSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (!CanMerge)
        {
            return;
        }

        string[] bookIds = SelectedIds();
        string[] groupIds = _catalogue.SelectedBooks
            .Select(book => book.WorkGroupId)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (groupIds.Length > 1)
        {
            _notifier.Notify(new UserNotification("Catalogue.Identity.MergeTwoWorks", UserNotificationSeverity.Warning));
            return;
        }

        await RunAsync(
            "identity_merge",
            async token =>
            {
                IReadOnlyDictionary<string, IReadOnlyList<string>> map =
                    await _occurrences.ResolveOccurrenceIdsAsync(bookIds, token).ConfigureAwait(true);
                string[] occurrenceIds = [.. map.Values.SelectMany(ids => ids).Distinct(StringComparer.Ordinal)];
                if (groupIds.Length == 0)
                {
                    return await _grouping.CreateAsync(IdentityGroupKind.Work, occurrenceIds, Actor, token).ConfigureAwait(true);
                }

                IdentityGroupDescriptor current = await _grouping.FindByIdAsync(groupIds[0], token).ConfigureAwait(true)
                    ?? throw new KeyNotFoundException("The work group no longer exists.");
                string[] joining = [.. occurrenceIds.Except(current.ActiveOccurrenceIds, StringComparer.Ordinal)];
                return joining.Length == 0
                    ? null
                    : await _grouping.MergeAsync(current.GroupId, joining, Actor, token).ConfigureAwait(true);
            },
            "Catalogue.Identity.Merged",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Splits the selected work back into separate books.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SplitSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSplit || _catalogue.SelectedBooks[0].WorkGroupId is not { } groupId)
        {
            return;
        }

        await RunAsync(
            "identity_split",
            async token =>
            {
                IdentityGroupDescriptor current = await _grouping.FindByIdAsync(groupId, token).ConfigureAwait(true)
                    ?? throw new KeyNotFoundException("The work group no longer exists.");
                return current.ActiveOccurrenceIds.Count == 0
                    ? null
                    : await _grouping.SplitAsync(groupId, current.ActiveOccurrenceIds, Actor, token).ConfigureAwait(true);
            },
            "Catalogue.Identity.Split",
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Builds the side-by-side comparison of every file of the selection and its work.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The comparison, or <see langword="null"/> when Compare is unavailable or failed.</returns>
    public async Task<CompareCopiesViewModel?> CreateCompareAsync(CancellationToken cancellationToken = default)
    {
        if (!CanCompare)
        {
            return null;
        }

        try
        {
            IReadOnlyList<BookFileFacts> files =
                await _occurrences.GetFileFactsAsync(SelectedIds(), cancellationToken).ConfigureAwait(true);
            return new CompareCopiesViewModel(files, _localization);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(IdentityGroupingViewModel), "identity_compare");
            _notifier.Notify(new UserNotification("Catalogue.Identity.CompareFailed"));
            return null;
        }
    }

    private async Task RunAsync(
        string operation,
        Func<CancellationToken, Task<IdentityGroupDescriptor?>> change,
        string doneKey,
        CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            IdentityGroupDescriptor? result = await change(cancellationToken).ConfigureAwait(true);
            await _catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
            if (result is null)
            {
                return;
            }

            bool used = false;
            _notifier.Notify(new UserNotification(
                doneKey,
                UserNotificationSeverity.Information,
                UserNotificationActionKind.Undo,
                async token =>
                {
                    if (used)
                    {
                        return;
                    }

                    used = true;
                    await UndoAsync(result, token).ConfigureAwait(true);
                }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            // The service refuses an occurrence that already belongs to another group, or a group
            // that disappeared: the library changed since the selection was made.
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(IdentityGroupingViewModel), operation);
            _notifier.Notify(new UserNotification("Catalogue.Identity.Conflict", UserNotificationSeverity.Warning));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(IdentityGroupingViewModel), operation);
            _notifier.Notify(new UserNotification("Catalogue.Identity.Failed"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UndoAsync(IdentityGroupDescriptor expected, CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            IdentityGroupDescriptor? current =
                await _grouping.FindByIdAsync(expected.GroupId, cancellationToken).ConfigureAwait(true);
            if (current is null || current.Version != expected.Version)
            {
                _notifier.Notify(new UserNotification("Catalogue.Identity.Conflict", UserNotificationSeverity.Warning));
                return;
            }

            await _grouping.UndoLastAsync(expected.GroupId, Actor, cancellationToken).ConfigureAwait(true);
            _notifier.Notify(new UserNotification("Catalogue.Identity.Undone", UserNotificationSeverity.Information));
            await _catalogue.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !ExceptionClassification.IsFatal(ex))
        {
            AppLog.ViewModelOperationFailed(_logger, ex, nameof(IdentityGroupingViewModel), "identity_undo");
            _notifier.Notify(new UserNotification("Catalogue.Identity.UndoFailed"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string[] SelectedIds() =>
        _catalogue.SelectedBooks.Select(book => book.BookId).Distinct(StringComparer.Ordinal).ToArray();

    private void SetBusy(bool value)
    {
        _isBusy = value;
        RaiseState();
    }

    private void OnSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_catalogue.IsRestoringSelection)
        {
            RaiseState();
        }
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanMerge));
        OnPropertyChanged(nameof(CanSplit));
        OnPropertyChanged(nameof(CanCompare));
    }

    private void RaiseText()
    {
        OnPropertyChanged(nameof(MergeText));
        OnPropertyChanged(nameof(SplitText));
        OnPropertyChanged(nameof(CompareText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
