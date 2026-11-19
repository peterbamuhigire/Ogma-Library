using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Inspector structure for Sept-23 Phase 11 slice 2: the four segmented sections, the overflow
/// menu labels and the per-book Show in folder action.
/// </summary>
public sealed partial class BookDetailViewModel
{
    private InspectorSection _selectedSection = InspectorSection.Overview;

    /// <summary>The section shown below the inspector header.</summary>
    public InspectorSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (_selectedSection != value)
            {
                _selectedSection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverviewSection));
                OnPropertyChanged(nameof(IsDetailsSection));
                OnPropertyChanged(nameof(IsContentsSection));
                OnPropertyChanged(nameof(IsActivitySection));
            }
        }
    }

    /// <summary>True when the Overview section is shown.</summary>
    public bool IsOverviewSection => _selectedSection == InspectorSection.Overview;

    /// <summary>True when the Details section is shown.</summary>
    public bool IsDetailsSection => _selectedSection == InspectorSection.Details;

    /// <summary>True when the Contents section is shown.</summary>
    public bool IsContentsSection => _selectedSection == InspectorSection.Contents;

    /// <summary>True when the Activity section is shown.</summary>
    public bool IsActivitySection => _selectedSection == InspectorSection.Activity;

    /// <summary>Localised Overview segment label.</summary>
    public string OverviewSectionText => _localization["Catalogue.BookDetail.Section.Overview"];

    /// <summary>Localised Details segment label.</summary>
    public string DetailsSectionText => _localization["Catalogue.BookDetail.Section.Details"];

    /// <summary>Localised Contents segment label.</summary>
    public string ContentsSectionText => _localization["Catalogue.BookDetail.Section.Contents"];

    /// <summary>Localised Activity segment label.</summary>
    public string ActivitySectionText => _localization["Catalogue.BookDetail.Section.Activity"];

    /// <summary>Localised accessible name of the inspector section switcher.</summary>
    public string SectionsAccessibleText => _localization["Catalogue.BookDetail.Section.Group"];

    /// <summary>Localised accessible name of the overflow ("More actions") button.</summary>
    public string MoreActionsText => _localization["Catalogue.BookDetail.More"];

    /// <summary>Localised "Technical details" expander header.</summary>
    public string TechnicalDetailsText => _localization["Catalogue.BookDetail.Technical"];

    /// <summary>Localised overflow item that adds the book to a collection.</summary>
    public string AddToShelfMenuText => _localization["Catalogue.BookDetail.AddToShelf"];

    /// <summary>Localised overflow item that opens the write-back preview.</summary>
    public string WriteMetadataMenuText => _localization["Catalogue.BookDetail.WriteMetadata"];

    /// <summary>Localised overflow item that reveals the PDF in the file manager.</summary>
    public string ShowInFolderText => _localization["Catalogue.BookDetail.ShowInFolder"];

    /// <summary>Localised AI card heading under Activity (content arrives in Phase 16, D-50).</summary>
    public string AiCardText => _localization["Catalogue.BookDetail.Tab.Ai"];

    /// <summary>Localised file size, e.g. "Size: 1,048,576 bytes"; empty when unknown.</summary>
    public string SizeDisplayText => _book?.SizeBytes is { } size
        ? string.Format(
            _localization.CurrentCulture,
            _localization["Catalogue.BookDetail.Technical.SizeFormat"],
            size.ToString("N0", _localization.CurrentCulture))
        : string.Empty;

    /// <summary>Index of <see cref="CurrentReadingStatus"/> for the status picker (enum order).</summary>
    public int CurrentReadingStatusIndex => (int)CurrentReadingStatus;

    /// <summary>True when the loaded book's PDF can be revealed in the file manager.</summary>
    public bool CanShowInFolder => _book is { IsAvailable: true } && _fileLocator is not null;

    /// <summary>
    /// Reveals the loaded book's PDF in the platform file manager. The path comes only from the
    /// catalogue-backed <c>IBookFileLocator</c>; a missing file shows the localised missing-file
    /// message instead.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ShowInFolderAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || _fileLocator is null)
        {
            return;
        }

        string bookId = _book.BookId;
        string? path = await _fileLocator.LocateAsync(bookId, cancellationToken).ConfigureAwait(false);
        if (path is null || !File.Exists(path))
        {
            UpdateOnUiThread(() => SetOpenErrorFor(bookId, OpenFileMissingKey));
            return;
        }

        RevealFile(path);
    }

    /// <summary>Test seam for <see cref="ShowInFolderAsync"/>; defaults to the platform file manager.</summary>
    internal Func<string, bool> RevealFile { get; set; } = FileManagerReveal.Reveal;

    /// <summary>Sets the reading status chosen by index in the status picker.</summary>
    /// <param name="index">The <see cref="ReadingStatus"/> value as an index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task SetReadingStatusIndexAsync(int index, CancellationToken cancellationToken = default) =>
        index < 0 || index == CurrentReadingStatusIndex || !Enum.IsDefined((ReadingStatus)index)
            ? Task.CompletedTask
            : SetReadingStatusAsync((ReadingStatus)index, cancellationToken);

    private void OnInspectorBookChanged()
    {
        OnPropertyChanged(nameof(SizeDisplayText));
        OnPropertyChanged(nameof(CurrentReadingStatusIndex));
        OnPropertyChanged(nameof(CanShowInFolder));
        OnFieldsBookChanged();
    }

    private void OnInspectorCultureChanged()
    {
        OnPropertyChanged(nameof(OverviewSectionText));
        OnPropertyChanged(nameof(DetailsSectionText));
        OnPropertyChanged(nameof(ContentsSectionText));
        OnPropertyChanged(nameof(ActivitySectionText));
        OnPropertyChanged(nameof(SectionsAccessibleText));
        OnPropertyChanged(nameof(MoreActionsText));
        OnPropertyChanged(nameof(TechnicalDetailsText));
        OnPropertyChanged(nameof(AddToShelfMenuText));
        OnPropertyChanged(nameof(WriteMetadataMenuText));
        OnPropertyChanged(nameof(ShowInFolderText));
        OnPropertyChanged(nameof(AiCardText));
        OnPropertyChanged(nameof(SizeDisplayText));
        OnFieldsCultureChanged();
    }
}
