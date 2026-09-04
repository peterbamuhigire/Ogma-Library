using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// View model for the book-detail panel (FR-CAT-004). Exposes all five metadata
/// field groups from <see cref="BookDetailProjection"/> and the "Read" / "Enrich"
/// action commands.
/// </summary>
public sealed class BookDetailViewModel : INotifyPropertyChanged
{
    private readonly ICatalogueReadModel _readModel;
    private readonly IReaderNavigationService _reader;
    private readonly ILocalizationService _localization;
    private readonly IBookMetadataEnrichmentService? _metadataEnrichment;
    private readonly IReadingMemoryService? _readingMemoryService;
    private readonly IOcrJobQueueService? _ocrJobs;
    private readonly IPasswordProvider? _passwordProvider;
    private readonly IBookCurationService? _curation;
    private readonly string? _assetRootPath;

    private BookDetailProjection? _book;
    private ReadingMemory? _editableReadingMemory;
    private bool _isLoading;
    private bool _isEnriching;
    private bool _isQueueingOcr;
    private bool _isSavingReadingMemory;
    private bool _isUpdatingCuration;
    private bool _isVisible;
    private string? _enrichmentStatusText;
    private string? _ocrStatusText;
    private string? _passwordStatusText;
    private string? _readingMemoryStatusText;
    private string? _curationStatusText;
    private string _readingMemoryOpenedBecause = string.Empty;
    private string _readingMemoryKeyInsight = string.Empty;
    private string _readingMemoryOpenQuestions = string.Empty;
    private string _readingMemoryDispositionText = string.Empty;

    /// <summary>
    /// Initializes a new instance of <see cref="BookDetailViewModel"/>.
    /// </summary>
    /// <param name="readModel">The catalogue read model.</param>
    /// <param name="reader">The reader navigation service.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="metadataEnrichment">The deterministic no-AI metadata enrichment service.</param>
    /// <param name="readingMemoryService">The reading-memory persistence service.</param>
    /// <param name="ocrJobs">The OCR queue service for scanned PDFs.</param>
    /// <param name="passwordProvider">The OS credential provider for protected PDFs.</param>
    /// <param name="curation">The durable personal curation service.</param>
    /// <param name="assetRootPath">The configured sidecar root used for local visual assets.</param>
    public BookDetailViewModel(
        ICatalogueReadModel readModel,
        IReaderNavigationService reader,
        ILocalizationService localization,
        IBookMetadataEnrichmentService? metadataEnrichment = null,
        IReadingMemoryService? readingMemoryService = null,
        IOcrJobQueueService? ocrJobs = null,
        IPasswordProvider? passwordProvider = null,
        IBookCurationService? curation = null,
        string? assetRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(localization);

        _readModel = readModel;
        _reader = reader;
        _localization = localization;
        _metadataEnrichment = metadataEnrichment;
        _readingMemoryService = readingMemoryService;
        _ocrJobs = ocrJobs;
        _passwordProvider = passwordProvider;
        _curation = curation;
        _assetRootPath = assetRootPath;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Visibility & loading ────────────────────────────────────────────────────

    /// <summary>True while the detail is loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True while deterministic provider metadata enrichment is running.</summary>
    public bool IsEnriching
    {
        get => _isEnriching;
        private set
        {
            if (_isEnriching != value)
            {
                _isEnriching = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEnrich));
            }
        }
    }

    /// <summary>True while an OCR queue request is running.</summary>
    public bool IsQueueingOcr
    {
        get => _isQueueingOcr;
        private set
        {
            if (_isQueueingOcr != value)
            {
                _isQueueingOcr = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRunOcr));
            }
        }
    }

    /// <summary>True when the detail panel should be shown.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True when the selected book can run deterministic metadata enrichment.</summary>
    public bool CanEnrich => _book is not null && _metadataEnrichment is not null && !IsEnriching;

    /// <summary>True when the selected book can be queued for OCR.</summary>
    public bool CanRunOcr => _book is not null && _ocrJobs is not null && !IsQueueingOcr;

    /// <summary>True when the selected protected book can forget a stored OS password.</summary>
    public bool CanForgetPassword =>
        _book is { IsPasswordProtected: true } &&
        _passwordProvider is not null &&
        !string.IsNullOrWhiteSpace(_book.Sha256Hash);

    /// <summary>True when the loaded book has a durable curation service available.</summary>
    public bool CanUpdateCuration => _book is not null && _curation is not null && !IsUpdatingCuration;

    /// <summary>True while a status, rating, or favourite update is being persisted.</summary>
    public bool IsUpdatingCuration
    {
        get => _isUpdatingCuration;
        private set
        {
            if (_isUpdatingCuration != value)
            {
                _isUpdatingCuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanUpdateCuration));
            }
        }
    }

    /// <summary>Localized button label for deterministic metadata enrichment.</summary>
    public string EnrichText => _localization["Catalogue.BookDetail.Enrich"];

    /// <summary>Localized tooltip for deterministic metadata enrichment.</summary>
    public string EnrichTooltip => _localization["Catalogue.BookDetail.EnrichTooltip"];

    /// <summary>Localized button label for OCR queueing.</summary>
    public string RunOcrText => _localization["Catalogue.BookDetail.RunOcr"];

    /// <summary>Localized tooltip for OCR queueing.</summary>
    public string RunOcrTooltip => _localization["Catalogue.BookDetail.RunOcrTooltip"];

    /// <summary>Localized button label for clearing a stored PDF password.</summary>
    public string ForgetPasswordText => _localization["Catalogue.BookDetail.ForgetPassword"];

    /// <summary>Localized tooltip for clearing a stored PDF password.</summary>
    public string ForgetPasswordTooltip => _localization["Catalogue.BookDetail.ForgetPasswordTooltip"];

    /// <summary>Localized curation status label.</summary>
    public string CurationStatusLabel => _localization["Catalogue.BookDetail.Curation.Status"];

    /// <summary>Localized curation rating label.</summary>
    public string CurationRatingLabel => _localization["Catalogue.BookDetail.Curation.Rating"];

    /// <summary>Localized unread status label.</summary>
    public string CurationUnreadText => _localization["Catalogue.BookDetail.Curation.Unread"];

    /// <summary>Localized reading status label.</summary>
    public string CurationReadingText => _localization["Catalogue.BookDetail.Curation.Reading"];

    /// <summary>Localized finished status label.</summary>
    public string CurationFinishedText => _localization["Catalogue.BookDetail.Curation.Finished"];

    /// <summary>Localized abandoned status label.</summary>
    public string CurationAbandonedText => _localization["Catalogue.BookDetail.Curation.Abandoned"];

    /// <summary>Localized favourite toggle label.</summary>
    public string FavouriteButtonText => _book?.IsFavourite == true
        ? _localization["Catalogue.BookDetail.Curation.RemoveFavourite"]
        : _localization["Catalogue.BookDetail.Curation.AddFavourite"];

    /// <summary>Current reading status, defaulting to unread when no progress exists.</summary>
    public ReadingStatus CurrentReadingStatus =>
        _book?.ReadingProgress is { } progress
            ? (ReadingStatus)progress.Status
            : OgmaLibrary.Domain.ReadingStatus.Unread;

    /// <summary>Current personal favourite state.</summary>
    public bool IsFavourite => _book?.IsFavourite == true;

    /// <summary>Current user-facing curation status, if any.</summary>
    public string? CurationStatusText
    {
        get => _curationStatusText;
        private set
        {
            if (_curationStatusText != value)
            {
                _curationStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCurationStatus));
            }
        }
    }

    /// <summary>True when a curation result message should be displayed.</summary>
    public bool HasCurationStatus => !string.IsNullOrWhiteSpace(CurationStatusText);

    /// <summary>Current user-facing enrichment status, if any.</summary>
    public string? EnrichmentStatusText
    {
        get => _enrichmentStatusText;
        private set
        {
            if (_enrichmentStatusText != value)
            {
                _enrichmentStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasEnrichmentStatus));
            }
        }
    }

    /// <summary>True when the detail panel has an enrichment status to display.</summary>
    public bool HasEnrichmentStatus => !string.IsNullOrWhiteSpace(EnrichmentStatusText);

    /// <summary>Current user-facing OCR queue status, if any.</summary>
    public string? OcrStatusText
    {
        get => _ocrStatusText;
        private set
        {
            if (_ocrStatusText != value)
            {
                _ocrStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasOcrStatus));
            }
        }
    }

    /// <summary>True when the detail panel has OCR queue status to display.</summary>
    public bool HasOcrStatus => !string.IsNullOrWhiteSpace(OcrStatusText);

    /// <summary>Current user-facing password action status, if any.</summary>
    public string? PasswordStatusText
    {
        get => _passwordStatusText;
        private set
        {
            if (_passwordStatusText != value)
            {
                _passwordStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPasswordStatus));
            }
        }
    }

    /// <summary>True when the detail panel has password status to display.</summary>
    public bool HasPasswordStatus => !string.IsNullOrWhiteSpace(PasswordStatusText);

    // ── Core identity ───────────────────────────────────────────────────────────

    /// <summary>The loaded book detail projection, or <see langword="null"/> if not loaded.</summary>
    public BookDetailProjection? Book
    {
        get => _book;
        private set
        {
            _book = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(AuthorsDisplay));
            OnPropertyChanged(nameof(Year));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Rating));
            OnPropertyChanged(nameof(Isbn));
            OnPropertyChanged(nameof(Doi));
            OnPropertyChanged(nameof(RelativePath));
            OnPropertyChanged(nameof(SizeBytes));
            OnPropertyChanged(nameof(Sha256Hash));
            OnPropertyChanged(nameof(IsOcrDerived));
            OnPropertyChanged(nameof(IsPasswordProtected));
            OnPropertyChanged(nameof(ReadingStatus));
            OnPropertyChanged(nameof(ReadingProgressPct));
            OnPropertyChanged(nameof(LastReadDisplay));
            OnPropertyChanged(nameof(AnnotationCount));
            OnPropertyChanged(nameof(ReadingMemorySummaryLabel));
            OnPropertyChanged(nameof(ReadingMemoryKeyInsightLabel));
            OnPropertyChanged(nameof(ReadingMemoryDispositionLabel));
            OnPropertyChanged(nameof(ReadingMemoryOpenedBecauseLabel));
            OnPropertyChanged(nameof(ReadingMemoryOpenQuestionsLabel));
            OnPropertyChanged(nameof(ReadingMemoryKeyInsightExcerpt));
            OnPropertyChanged(nameof(ReadingMemoryDispositionDisplay));
            OnPropertyChanged(nameof(HasReadingMemorySummary));
            OnPropertyChanged(nameof(CanEditReadingMemory));
            OnPropertyChanged(nameof(CanSaveReadingMemory));
            OnPropertyChanged(nameof(FileFields));
            OnPropertyChanged(nameof(BiblioFields));
            OnPropertyChanged(nameof(BiblioFieldDisplayRows));
            OnPropertyChanged(nameof(ReadingFields));
            OnPropertyChanged(nameof(EnrichmentFields));
            OnPropertyChanged(nameof(EnrichmentFieldDisplayRows));
            OnPropertyChanged(nameof(AiFields));
            OnPropertyChanged(nameof(CanEnrich));
            OnPropertyChanged(nameof(CanRunOcr));
            OnPropertyChanged(nameof(CanForgetPassword));
            OnPropertyChanged(nameof(CanUpdateCuration));
            OnPropertyChanged(nameof(CurrentReadingStatus));
            OnPropertyChanged(nameof(IsFavourite));
            OnPropertyChanged(nameof(FavouriteButtonText));
        }
    }

    // ── Bibliographic group ────────────────────────────────────────────────────

    /// <summary>Book title.</summary>
    public string? Title => _book?.Title;

    /// <summary>Authors joined for display.</summary>
    public string? AuthorsDisplay => _book?.Authors.Count > 0
        ? string.Join("; ", _book.Authors)
        : null;

    /// <summary>Publication year as string.</summary>
    public string? Year => _book?.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>ISBN display string.</summary>
    public string? Isbn => _book?.Isbn;

    /// <summary>DOI display string.</summary>
    public string? Doi => _book?.Doi;

    // ── File group ────────────────────────────────────────────────────────────

    /// <summary>Library-relative file path.</summary>
    public string? RelativePath => _book?.RelativePath;

    /// <summary>File size in bytes.</summary>
    public long? SizeBytes => _book?.SizeBytes;

    /// <summary>SHA-256 hex digest.</summary>
    public string? Sha256Hash => _book?.Sha256Hash;

    /// <summary>Configured sidecar root used only for local cover loading.</summary>
    public string? AssetRootPath => _assetRootPath;

    /// <summary>Whether the loaded book has OCR-derived searchable text.</summary>
    public bool IsOcrDerived => _book?.IsOcrDerived == true;

    /// <summary>Whether the loaded book's PDF requires a password.</summary>
    public bool IsPasswordProtected => _book?.IsPasswordProtected == true;

    // ── Reading group ─────────────────────────────────────────────────────────

    /// <summary>Lifecycle status.</summary>
    public int? Status => _book?.Status;

    /// <summary>Reader rating 1–5.</summary>
    public int? Rating => _book?.Rating;

    /// <summary>Reading status from progress record.</summary>
    public int? ReadingStatus => _book?.ReadingProgress?.Status;

    /// <summary>Reading completion percentage.</summary>
    public double? ReadingProgressPct => _book?.ReadingProgress?.CompletionPct;

    /// <summary>Last-read date formatted for display.</summary>
    public string? LastReadDisplay => _book?.ReadingProgress?.LastReadUtc
        ?.ToLocalTime()
        .ToString("d", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Number of annotations.</summary>
    public int? AnnotationCount => _book?.Annotations;

    /// <summary>Localized reading-memory summary label.</summary>
    public string ReadingMemorySummaryLabel => _localization["Catalogue.BookDetail.ReadingMemory"];

    /// <summary>Localized reading-memory key-insight label.</summary>
    public string ReadingMemoryKeyInsightLabel => _localization["Catalogue.BookDetail.ReadingMemoryKeyInsight"];

    /// <summary>Localized reading-memory disposition label.</summary>
    public string ReadingMemoryDispositionLabel => _localization["Catalogue.BookDetail.ReadingMemoryDisposition"];

    /// <summary>Localized reading-memory save button label.</summary>
    public string ReadingMemorySaveLabel => _localization["ReadingMemory.Save"];

    /// <summary>Localized reading-memory "opened because" label.</summary>
    public string ReadingMemoryOpenedBecauseLabel => _localization["ReadingMemory.OpenedBecause"];

    /// <summary>Localized reading-memory open-questions label.</summary>
    public string ReadingMemoryOpenQuestionsLabel => _localization["ReadingMemory.OpenQuestions"];

    /// <summary>True when the loaded book has memory content worth summarizing.</summary>
    public bool HasReadingMemorySummary =>
        _book?.ReadingMemory is { } memory &&
        (memory.Disposition is not null || !string.IsNullOrWhiteSpace(memory.KeyInsight));

    /// <summary>Key insight truncated for the compact book-detail reading card.</summary>
    public string ReadingMemoryKeyInsightExcerpt =>
        Truncate(_book?.ReadingMemory?.KeyInsight, 80) ??
        _localization["Catalogue.BookDetail.ReadingMemoryEmpty"];

    /// <summary>Disposition score formatted for the compact reading-memory summary.</summary>
    public string ReadingMemoryDispositionDisplay =>
        _book?.ReadingMemory?.Disposition is int disposition
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Catalogue.BookDetail.ReadingMemoryDispositionFormat"],
                disposition)
            : _localization["Catalogue.BookDetail.ReadingMemoryEmpty"];

    /// <summary>True when the book-detail panel can edit reading-memory fields.</summary>
    public bool CanEditReadingMemory => _book is not null && _readingMemoryService is not null;

    /// <summary>True when the book-detail reading-memory save action is available.</summary>
    public bool CanSaveReadingMemory => CanEditReadingMemory && !IsSavingReadingMemory;

    /// <summary>True while a reading-memory save is running.</summary>
    public bool IsSavingReadingMemory
    {
        get => _isSavingReadingMemory;
        private set
        {
            if (_isSavingReadingMemory != value)
            {
                _isSavingReadingMemory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveReadingMemory));
            }
        }
    }

    /// <summary>Status text for book-detail reading-memory saves.</summary>
    public string? ReadingMemoryStatusText
    {
        get => _readingMemoryStatusText;
        private set
        {
            if (_readingMemoryStatusText != value)
            {
                _readingMemoryStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasReadingMemoryStatus));
            }
        }
    }

    /// <summary>True when there is reading-memory save status to display.</summary>
    public bool HasReadingMemoryStatus => !string.IsNullOrWhiteSpace(ReadingMemoryStatusText);

    /// <summary>Editable "opened because" value for the book-detail memory panel.</summary>
    public string ReadingMemoryOpenedBecause
    {
        get => _readingMemoryOpenedBecause;
        set
        {
            if (_readingMemoryOpenedBecause != value)
            {
                _readingMemoryOpenedBecause = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Editable key insight value for the book-detail memory panel.</summary>
    public string ReadingMemoryKeyInsight
    {
        get => _readingMemoryKeyInsight;
        set
        {
            if (_readingMemoryKeyInsight != value)
            {
                _readingMemoryKeyInsight = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Editable open questions value for the book-detail memory panel.</summary>
    public string ReadingMemoryOpenQuestions
    {
        get => _readingMemoryOpenQuestions;
        set
        {
            if (_readingMemoryOpenQuestions != value)
            {
                _readingMemoryOpenQuestions = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Editable disposition value for the book-detail memory panel.</summary>
    public string ReadingMemoryDispositionText
    {
        get => _readingMemoryDispositionText;
        set
        {
            if (_readingMemoryDispositionText != value)
            {
                _readingMemoryDispositionText = value;
                OnPropertyChanged();
            }
        }
    }

    // ── Five field groups for the detail panel tabs ───────────────────────────

    /// <summary>File metadata fields (group 1).</summary>
    public IReadOnlyList<MetadataFieldProjection> FileFields =>
        _book?.MetadataFields
            .Where(f => FileFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Bibliographic metadata fields (group 2).</summary>
    public IReadOnlyList<MetadataFieldProjection> BiblioFields =>
        _book?.MetadataFields
            .Where(f => BiblioFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Formatted bibliographic metadata rows for display.</summary>
    public IReadOnlyList<string> BiblioFieldDisplayRows =>
        BiblioFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(FormatField)
            .ToList();

    /// <summary>Reading state fields (group 3).</summary>
    public IReadOnlyList<MetadataFieldProjection> ReadingFields =>
        _book?.MetadataFields
            .Where(f => ReadingFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Enrichment fields from metadata providers (group 4).</summary>
    public IReadOnlyList<MetadataFieldProjection> EnrichmentFields =>
        _book?.MetadataFields
            .Where(IsProviderEnrichedField)
            .ToList() ?? [];

    /// <summary>Formatted provider-sourced metadata rows with provenance for display.</summary>
    public IReadOnlyList<string> EnrichmentFieldDisplayRows =>
        EnrichmentFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(FormatField)
            .ToList();

    /// <summary>AI-generated fields (group 5).</summary>
    public IReadOnlyList<MetadataFieldProjection> AiFields =>
        _book?.MetadataFields
            .Where(f => AiFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the PDF reader for the currently loaded book through the shell
    /// navigation service.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task OpenReaderAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            return;
        }

        await _reader.OpenReaderAsync(_book.BookId, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Persists a personal reading status and refreshes the detail projection.</summary>
    public Task SetReadingStatusAsync(
        ReadingStatus status,
        CancellationToken cancellationToken = default) =>
        UpdateCurationAsync(readingStatus: status, cancellationToken: cancellationToken);

    /// <summary>Persists a validated personal rating and refreshes the detail projection.</summary>
    public Task SetRatingAsync(int rating, CancellationToken cancellationToken = default) =>
        UpdateCurationAsync(rating: rating, cancellationToken: cancellationToken);

    /// <summary>Toggles the personal favourite flag and refreshes the detail projection.</summary>
    public Task ToggleFavouriteAsync(CancellationToken cancellationToken = default) =>
        UpdateCurationAsync(isFavourite: !IsFavourite, cancellationToken: cancellationToken);

    /// <summary>Runs deterministic provider metadata enrichment for the loaded book.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task EnrichMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            return;
        }

        if (_metadataEnrichment is null)
        {
            EnrichmentStatusText = _localization["Catalogue.BookDetail.EnrichUnavailable"];
            return;
        }

        string bookId = _book.BookId;
        IsEnriching = true;
        EnrichmentStatusText = _localization["Catalogue.BookDetail.Enriching"];

        try
        {
            (bool success, string? errorMessage) = await _metadataEnrichment
                .EnrichAsync(bookId, absoluteFilePath: null, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                UpdateOnUiThread(() =>
                {
                    EnrichmentStatusText = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        _localization["Catalogue.BookDetail.EnrichFailedFormat"],
                        errorMessage ?? _localization["Catalogue.BookDetail.EnrichUnknownError"]);
                    IsEnriching = false;
                });
                return;
            }

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);

            UpdateOnUiThread(() =>
            {
                Book = detail;
                EnrichmentStatusText = _localization["Catalogue.BookDetail.EnrichComplete"];
                IsEnriching = false;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateOnUiThread(() =>
            {
                EnrichmentStatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Catalogue.BookDetail.EnrichFailedFormat"],
                    ex.Message);
                IsEnriching = false;
            });
        }
    }

    /// <summary>Queues OCR for the loaded book so scanned pages become searchable.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RunOcrAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || IsQueueingOcr)
        {
            return;
        }

        if (_ocrJobs is null)
        {
            OcrStatusText = _localization["Catalogue.BookDetail.OcrUnavailable"];
            return;
        }

        string bookId = _book.BookId;
        IsQueueingOcr = true;
        OcrStatusText = _localization["Catalogue.BookDetail.OcrQueueing"];

        try
        {
            OcrQueueResult result = await _ocrJobs.QueueBookAsync(bookId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            UpdateOnUiThread(() =>
            {
                OcrStatusText = result switch
                {
                    { Queued: true } => _localization["Catalogue.BookDetail.OcrQueued"],
                    { AlreadyQueued: true } => _localization["Catalogue.BookDetail.OcrAlreadyQueued"],
                    _ => string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        _localization["Catalogue.BookDetail.OcrFailedFormat"],
                        result.ErrorMessage ?? _localization["Catalogue.BookDetail.EnrichUnknownError"]),
                };
                IsQueueingOcr = false;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateOnUiThread(() =>
            {
                OcrStatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Catalogue.BookDetail.OcrFailedFormat"],
                    ex.Message);
                IsQueueingOcr = false;
            });
        }
    }

    /// <summary>Forgets the stored OS credential for the loaded protected PDF.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ForgetPasswordAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || _passwordProvider is null || string.IsNullOrWhiteSpace(_book.Sha256Hash))
        {
            PasswordStatusText = _localization["Catalogue.BookDetail.ForgetPasswordUnavailable"];
            return;
        }

        try
        {
            await _passwordProvider
                .ForgetPasswordAsync(
                    new PasswordRequest(_book.BookId, _book.Sha256Hash, _book.Title),
                    cancellationToken)
                .ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                PasswordStatusText = _localization["Catalogue.BookDetail.ForgetPasswordComplete"];
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateOnUiThread(() =>
            {
                PasswordStatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Catalogue.BookDetail.ForgetPasswordFailedFormat"],
                    ex.Message);
            });
        }
    }

    /// <summary>Saves the book-detail reading-memory fields and refreshes the summary projection.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SaveReadingMemoryAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || _readingMemoryService is null || IsSavingReadingMemory)
        {
            return;
        }

        string bookId = _book.BookId;
        int? disposition = null;
        if (!string.IsNullOrWhiteSpace(ReadingMemoryDispositionText))
        {
            if (!int.TryParse(
                    ReadingMemoryDispositionText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out int parsed) ||
                parsed is < 1 or > 5)
            {
                ReadingMemoryStatusText = _localization["ReadingMemory.InvalidDisposition"];
                return;
            }

            disposition = parsed;
        }

        DateTimeOffset createdAt = _editableReadingMemory?.CreatedAtUtc ?? DateTimeOffset.UtcNow;
        var memory = new ReadingMemory
        {
            BookId = bookId,
            OpenedBecause = NullIfWhiteSpace(ReadingMemoryOpenedBecause),
            KeyInsight = NullIfWhiteSpace(ReadingMemoryKeyInsight),
            OpenQuestions = NullIfWhiteSpace(ReadingMemoryOpenQuestions),
            Disposition = disposition,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        IsSavingReadingMemory = true;
        ReadingMemoryStatusText = null;

        try
        {
            await _readingMemoryService.SaveAsync(memory, cancellationToken)
                .ConfigureAwait(false);

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);

            UpdateOnUiThread(() =>
            {
                _editableReadingMemory = memory;
                Book = detail ?? _book;
                ReadingMemoryStatusText = _localization["ReadingMemory.Saved"];
                IsSavingReadingMemory = false;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateOnUiThread(() =>
            {
                ReadingMemoryStatusText = ex.Message;
                IsSavingReadingMemory = false;
            });
        }
    }

    private async Task UpdateCurationAsync(
        ReadingStatus? readingStatus = null,
        int? rating = null,
        bool? isFavourite = null,
        CancellationToken cancellationToken = default)
    {
        if (_book is null || _curation is null || IsUpdatingCuration)
        {
            return;
        }

        IsUpdatingCuration = true;
        CurationStatusText = null;
        try
        {
            await _curation.UpdateReadingStateAsync(
                _book.BookId,
                readingStatus,
                rating,
                isFavourite,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(_book.BookId, cancellationToken)
                .ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                Book = detail ?? _book;
                CurationStatusText = _localization["Catalogue.BookDetail.Curation.Saved"];
                IsUpdatingCuration = false;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateOnUiThread(() =>
            {
                CurationStatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Catalogue.BookDetail.Curation.FailedFormat"],
                    ex.Message);
                IsUpdatingCuration = false;
            });
        }
    }

    /// <summary>Closes the detail panel.</summary>
    public void Close()
    {
        IsVisible = false;
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the book detail for the given book ID and makes the panel visible.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task LoadBookAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        IsLoading = true;
        IsVisible = true;

        try
        {
            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);
            ReadingMemory? memory = await LoadEditableReadingMemoryAsync(detail, cancellationToken)
                .ConfigureAwait(false);

            UpdateOnUiThread(() =>
            {
                Book = detail;
                SetEditableReadingMemory(memory);
                IsLoading = false;
            });
        }
        catch
        {
            UpdateOnUiThread(() => IsLoading = false);
            throw;
        }
    }

    /// <summary>
    /// Refreshes the currently loaded book without changing panel visibility.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task RefreshLoadedBookAsync(CancellationToken cancellationToken = default)
    {
        string? bookId = _book?.BookId;
        if (string.IsNullOrWhiteSpace(bookId))
        {
            return;
        }

        BookDetailProjection? detail = await _readModel
            .GetBookDetailAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        ReadingMemory? memory = await LoadEditableReadingMemoryAsync(detail, cancellationToken)
            .ConfigureAwait(false);

        UpdateOnUiThread(() =>
        {
            Book = detail;
            SetEditableReadingMemory(memory);
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static void UpdateOnUiThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }

    // ── Field group membership sets ───────────────────────────────────────────

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, maxLength - 3), "...");
    }

    private async Task<ReadingMemory?> LoadEditableReadingMemoryAsync(
        BookDetailProjection? detail,
        CancellationToken cancellationToken)
    {
        if (detail is null || _readingMemoryService is null)
        {
            return null;
        }

        return await _readingMemoryService.LoadAsync(detail.BookId, cancellationToken)
            .ConfigureAwait(false);
    }

    private void SetEditableReadingMemory(ReadingMemory? memory)
    {
        _editableReadingMemory = memory;
        ReadingMemoryOpenedBecause = memory?.OpenedBecause ?? string.Empty;
        ReadingMemoryKeyInsight = memory?.KeyInsight ?? string.Empty;
        ReadingMemoryOpenQuestions = memory?.OpenQuestions ?? string.Empty;
        ReadingMemoryDispositionText = memory?.Disposition?.ToString(
            System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        ReadingMemoryStatusText = null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatField(MetadataFieldProjection field)
    {
        string value = string.IsNullOrWhiteSpace(field.Value) ? "-" : field.Value.Trim();
        string provenance = FormatProvenance(field);
        return string.IsNullOrWhiteSpace(provenance)
            ? $"{field.FieldName}: {value}"
            : $"{field.FieldName}: {value} ({provenance})";
    }

    private static string FormatProvenance(MetadataFieldProjection field)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(field.Source))
        {
            parts.Add(field.Source.Trim());
        }

        if (field.Confidence is double confidence)
        {
            parts.Add(confidence.ToString("P0", System.Globalization.CultureInfo.CurrentCulture));
        }

        if (field.IsOverridden)
        {
            parts.Add("manual");
        }

        return string.Join(", ", parts);
    }

    private static bool IsProviderEnrichedField(MetadataFieldProjection field)
    {
        if (string.IsNullOrWhiteSpace(field.Source))
        {
            return false;
        }

        return !field.IsOverridden &&
            !string.Equals(field.Source, "System", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field.Source, "User", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field.Source, "Local", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> FileFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "FileName", "RelativePath", "SizeBytes", "ModifiedUtc",
            "Format", "Pages", "PdfVersion", "IsEncrypted",
        };

    private static readonly HashSet<string> BiblioFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Title", "Author", "Publisher", "Year", "Isbn", "Doi",
            "Language", "Categories", "Description",
        };

    private static readonly HashSet<string> ReadingFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Status", "Rating", "Tags", "ReadingProgressPct", "LastReadDate",
        };

    private static readonly HashSet<string> AiFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "AiDescription", "RecommendedReadingLevel", "RelatedTitles",
        };
}
